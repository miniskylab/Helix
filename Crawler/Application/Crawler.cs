using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CrawlerBackendBusiness
{
    public static class Crawler
    {
        static int _activeThreadCount;
        static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        static Configurations _configurations;
        static Task[] _tasks;
        static readonly ConcurrentDictionary<string, bool> AlreadyVerifiedUrls = new ConcurrentDictionary<string, bool>();
        static readonly string WorkingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        static readonly string ErrorFilePath = Path.Combine(WorkingDirectory, "error.txt");
        static readonly BlockingCollection<ResourceCollector> ResourceCollectorPool = new BlockingCollection<ResourceCollector>();
        static readonly BlockingCollection<ResourceVerifier> ResourceVerifierPool = new BlockingCollection<ResourceVerifier>();
        static readonly object StaticLock = new object();
        static readonly BlockingCollection<RawResource> TobeVerifiedRawResources = new BlockingCollection<RawResource>();

        public static CrawlerState State { get; private set; }
        public static CancellationToken CancellationToken => _cancellationTokenSource.Token;

        static Crawler() { EnsureErrorLogFileIsRecreated(); }

        public static void StartWorking(Configurations configurations)
        {
            lock (StaticLock)
            {
                if (State != CrawlerState.Ready) return;
                State = CrawlerState.Working;
            }

            _configurations = configurations;
            _cancellationTokenSource = new CancellationTokenSource();
            _tasks = new Task[configurations.MaxThreadCount];
            _activeThreadCount = 0;

            try
            {
                lock (StaticLock)
                {
                    AlreadyVerifiedUrls.TryAdd(_configurations.StartUrl, true);
                    TobeVerifiedRawResources.Add(new RawResource { Url = _configurations.StartUrl, ParentUrl = null });
                }
                InitializeResourceCollectorPool();
                InitializeResourceVerifierPool();
                DoWorkInParallel();
            }
            catch (Exception exception)
            {
                File.WriteAllText(ErrorFilePath, exception.ToString());
            }
            finally
            {
                StopWorking();
            }
        }

        public static void StopWorking()
        {
            lock (StaticLock)
            {
                if (State == CrawlerState.Ready) return;
                State = CrawlerState.Ready;
            }

            if (_tasks != null && _tasks.Any())
            {
                _cancellationTokenSource.Cancel();
                Task.WhenAll(_tasks).Wait();
                _cancellationTokenSource.Dispose();
                _tasks = null;
            }

            while (ResourceCollectorPool.Any()) ResourceCollectorPool.Take().Dispose();
            while (ResourceVerifierPool.Any()) ResourceVerifierPool.Take().Dispose();
            while (TobeVerifiedRawResources.Any()) TobeVerifiedRawResources.Take();
            lock (StaticLock) { AlreadyVerifiedUrls.Clear(); }
            _activeThreadCount = 0;
        }

        static void Crawl()
        {
            while (TobeVerifiedRawResources.Any() || _activeThreadCount > 0)
            {
                Thread.Sleep(100);
                if (_cancellationTokenSource.IsCancellationRequested) return;
                while (TobeVerifiedRawResources.TryTake(out var tobeVerifiedRawResource))
                {
                    try
                    {
                        Interlocked.Increment(ref _activeThreadCount);
                        var verificationResult = ResourceVerifierPool.Take(_cancellationTokenSource.Token).Verify(tobeVerifiedRawResource);
                        if (verificationResult.IsBrokenResource || !verificationResult.IsInternalResource)
                        {
                            Interlocked.Decrement(ref _activeThreadCount);
                            continue;
                        }
                        ResourceCollectorPool.Take(_cancellationTokenSource.Token).CollectNewRawResourcesFrom(verificationResult.Resource);
                        Interlocked.Decrement(ref _activeThreadCount);
                        if (_cancellationTokenSource.IsCancellationRequested) return;
                    }
                    catch (OperationCanceledException operationCanceledException)
                    {
                        if (operationCanceledException.CancellationToken.IsCancellationRequested) return;
                        throw;
                    }
                }
            }
        }

        static void DoWorkInParallel()
        {
            for (var taskId = 0; taskId < _configurations.MaxThreadCount; taskId++) _tasks[taskId] = Task.Factory.StartNew(Crawl);
            Task.WhenAll(_tasks).Wait();
        }

        static void EnsureErrorLogFileIsRecreated()
        {
            if (File.Exists(ErrorFilePath)) File.Delete(ErrorFilePath);
        }

        static void InitializeResourceCollectorPool()
        {
            for (var resourceCollectorId = 0; resourceCollectorId < _configurations.MaxThreadCount; resourceCollectorId++)
            {
                var resourceCollector = new ResourceCollector(_configurations);
                resourceCollector.OnRawResourceCollected += rawResource =>
                {
                    lock (StaticLock)
                    {
                        if (AlreadyVerifiedUrls.TryGetValue(rawResource.Url.StripFragment(), out _)) return;
                        AlreadyVerifiedUrls.TryAdd(rawResource.Url.StripFragment(), true);
                    }
                    TobeVerifiedRawResources.Add(rawResource);
                };
                resourceCollector.OnExceptionOccurred += (exception, resource) =>
                {
                    /* TODO: How and Where do we log this information? */
                };
                resourceCollector.OnAllAttemptsToCollectNewRawResourcesFailed += parentResource =>
                {
                    /* TODO: How and Where do we log this information? */
                };
                resourceCollector.OnIdle += () => ResourceCollectorPool.Add(resourceCollector);
                ResourceCollectorPool.Add(resourceCollector);
            }
        }

        static void InitializeResourceVerifierPool()
        {
            for (var resourceVerifierId = 0; resourceVerifierId < _configurations.MaxThreadCount; resourceVerifierId++)
            {
                var resourceVerifier = new ResourceVerifier(_configurations);
                resourceVerifier.OnIdle += () => ResourceVerifierPool.Add(resourceVerifier);
                ResourceVerifierPool.Add(resourceVerifier);
            }
        }
    }
}