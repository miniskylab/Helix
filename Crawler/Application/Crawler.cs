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
        static Configurations _configurations;
        static Task[] _tasks;
        static readonly ConcurrentDictionary<string, bool> AlreadyVerifiedUrls = new ConcurrentDictionary<string, bool>();
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        static readonly BlockingCollection<ResourceCollector> ResourceCollectorPool = new BlockingCollection<ResourceCollector>();
        static readonly BlockingCollection<ResourceVerifier> ResourceVerifierPool = new BlockingCollection<ResourceVerifier>();
        static readonly object StaticLock = new object();
        static readonly BlockingCollection<RawResource> TobeVerifiedRawResources = new BlockingCollection<RawResource>();

        public static void StartWorking(Configurations configurations)
        {
            _configurations = configurations;
            _tasks = new Task[configurations.MaxThreadCount];

            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var errorFilePath = Path.Combine(workingDirectory, "error.txt");

            try
            {
                if (File.Exists(errorFilePath)) File.Delete(errorFilePath);
                lock (StaticLock) { AlreadyVerifiedUrls.TryAdd(_configurations.StartUrl, true); }
                TobeVerifiedRawResources.Add(new RawResource { Url = _configurations.StartUrl, ParentUrl = null });
                InitializeResourceCollectorPool();
                InitializeResourceVerifierPool();
                DoWorkInParallel();
            }
            catch (Exception exception)
            {
                File.WriteAllText(errorFilePath, exception.ToString());
            }
            finally
            {
                StopWorking();
            }
        }

        public static void StopWorking()
        {
            if (_tasks != null)
            {
                CancellationTokenSource.Cancel();
                Task.WhenAll(_tasks).Wait();
            }

            foreach (var resourceCollector in ResourceCollectorPool) resourceCollector.Dispose();
            foreach (var resourceVerifier in ResourceVerifierPool) resourceVerifier.Dispose();
        }

        static void Crawl()
        {
            while (TobeVerifiedRawResources.Any() || _activeThreadCount > 0)
            {
                Thread.Sleep(100);
                while (TobeVerifiedRawResources.TryTake(out var tobeVerifiedRawResource))
                {
                    Interlocked.Increment(ref _activeThreadCount);
                    var verificationResult = ResourceVerifierPool.Take(CancellationTokenSource.Token).Verify(tobeVerifiedRawResource);
                    if (verificationResult.IsBrokenResource || !verificationResult.IsInternalResource)
                    {
                        Interlocked.Decrement(ref _activeThreadCount);
                        continue;
                    }
                    ResourceCollectorPool.Take(CancellationTokenSource.Token).CollectNewRawResourcesFrom(verificationResult.Resource);
                    Interlocked.Decrement(ref _activeThreadCount);
                    if (CancellationTokenSource.IsCancellationRequested) return;
                }
                if (CancellationTokenSource.IsCancellationRequested) return;
            }
        }

        static void DoWorkInParallel()
        {
            for (var taskId = 0; taskId < _configurations.MaxThreadCount; taskId++) _tasks[taskId] = Task.Factory.StartNew(Crawl);
            Task.WhenAll(_tasks).Wait();
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