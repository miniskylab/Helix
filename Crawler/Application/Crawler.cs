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
        static int _activeWebBrowserCount;
        static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        static Configurations _configurations;
        static Task[] _crawlingTasks = { };
        static Task _mainWorkingTask;
        static readonly ConcurrentDictionary<string, bool> AlreadyVerifiedUrls = new ConcurrentDictionary<string, bool>();
        static readonly string WorkingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        static readonly string ErrorFilePath = Path.Combine(WorkingDirectory, "error.txt");
        static readonly BlockingCollection<ResourceCollector> ResourceCollectorPool = new BlockingCollection<ResourceCollector>();
        static readonly BlockingCollection<ResourceVerifier> ResourceVerifierPool = new BlockingCollection<ResourceVerifier>();
        static readonly object StaticLock = new object();
        static readonly BlockingCollection<RawResource> TobeVerifiedRawResources = new BlockingCollection<RawResource>();

        public static CrawlerState State { get; private set; } = CrawlerState.Ready;
        public static CancellationToken CancellationToken => _cancellationTokenSource.Token;
        public static int IdleWebBrowserCount => _configurations.WebBrowserCount - _activeWebBrowserCount;
        public static int RemainingUrlCount => TobeVerifiedRawResources.Count;
        public static int VerifiedUrlCount => AlreadyVerifiedUrls.Count - TobeVerifiedRawResources.Count;

        public static event ResourceVerifiedEvent OnResourceVerified;
        public static event StoppedEvent OnStopped;
        public static event WebBrowserClosedEvent OnWebBrowserClosed;
        public static event WebBrowserOpenedEvent OnWebBrowserOpened;

        static Crawler() { EnsureErrorLogFileIsRecreated(); }

        public static void Dispose()
        {
            StopWorking();
            _cancellationTokenSource?.Dispose();
        }

        public static void StartWorking(Configurations configurations)
        {
            lock (StaticLock)
            {
                if (State == CrawlerState.Unknown || State != CrawlerState.Ready) return;
                State = CrawlerState.Working;
            }

            _configurations = configurations;
            _cancellationTokenSource = new CancellationTokenSource();
            _crawlingTasks = new Task[configurations.WebBrowserCount];
            _activeWebBrowserCount = 0;
            _mainWorkingTask = Task.Run(() =>
            {
                try
                {
                    while (TobeVerifiedRawResources.Any()) TobeVerifiedRawResources.Take();
                    lock (StaticLock)
                    {
                        AlreadyVerifiedUrls.Clear();
                        AlreadyVerifiedUrls.TryAdd(_configurations.StartUrl, true);
                        TobeVerifiedRawResources.Add(new RawResource { Url = _configurations.StartUrl, ParentUrl = null });
                    }
                    InitializeResourceCollectorPool();
                    InitializeResourceVerifierPool();
                    DoWorkInParallel();
                }
                catch (Exception exception)
                {
                    if (exception is TaskCanceledException && _cancellationTokenSource.Token.IsCancellationRequested) return;
                    File.WriteAllText(ErrorFilePath, exception.ToString());
                }
                finally
                {
                    Task.Run(StopWorking);
                }
            });
        }

        public static void StopWorking()
        {
            lock (StaticLock)
            {
                if (State == CrawlerState.Unknown || State == CrawlerState.Ready) return;
                State = CrawlerState.Ready;
            }

            _cancellationTokenSource.Cancel();
            _mainWorkingTask.Wait();
            if (_crawlingTasks != null && _crawlingTasks.Any())
            {
                Task.WhenAll(_crawlingTasks.Where(task => task != null)).Wait();
                _crawlingTasks = null;
            }

            var isAllWorkDone = !TobeVerifiedRawResources.Any() && _activeWebBrowserCount == 0;
            while (ResourceVerifierPool.Any()) ResourceVerifierPool.Take().Dispose();
            while (ResourceCollectorPool.Any())
            {
                ResourceCollectorPool.Take().Dispose();
                OnWebBrowserClosed?.Invoke(_configurations.WebBrowserCount - ResourceCollectorPool.Count);
            }
            ReportWriter.Instance.Dispose();
            _activeWebBrowserCount = 0;
            OnStopped?.Invoke(isAllWorkDone);
        }

        static void Crawl()
        {
            while (TobeVerifiedRawResources.Any() || _activeWebBrowserCount > 0)
            {
                Thread.Sleep(100);
                if (_cancellationTokenSource.IsCancellationRequested) return;
                while (TobeVerifiedRawResources.TryTake(out var tobeVerifiedRawResource))
                {
                    try
                    {
                        Interlocked.Increment(ref _activeWebBrowserCount);
                        var verificationResult = ResourceVerifierPool.Take(_cancellationTokenSource.Token).Verify(tobeVerifiedRawResource);
                        ReportWriter.Instance.WriteReport(verificationResult, _configurations.ReportBrokenLinksOnly);
                        OnResourceVerified?.Invoke(verificationResult);

                        if (verificationResult.IsBrokenResource || !verificationResult.IsInternalResource)
                        {
                            Interlocked.Decrement(ref _activeWebBrowserCount);
                            continue;
                        }
                        ResourceCollectorPool.Take(_cancellationTokenSource.Token).CollectNewRawResourcesFrom(verificationResult.Resource);
                        Interlocked.Decrement(ref _activeWebBrowserCount);
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
            for (var taskId = 0; taskId < _configurations.WebBrowserCount; taskId++) _crawlingTasks[taskId] = Task.Factory.StartNew(Crawl);
            Task.WhenAll(_crawlingTasks).Wait();
        }

        static void EnsureErrorLogFileIsRecreated()
        {
            if (File.Exists(ErrorFilePath)) File.Delete(ErrorFilePath);
        }

        static void InitializeResourceCollectorPool()
        {
            for (var resourceCollectorId = 0; resourceCollectorId < _configurations.WebBrowserCount; resourceCollectorId++)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested) throw new TaskCanceledException();
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
                OnWebBrowserOpened?.Invoke(resourceCollectorId + 1);
            }
        }

        static void InitializeResourceVerifierPool()
        {
            for (var resourceVerifierId = 0; resourceVerifierId < _configurations.WebBrowserCount; resourceVerifierId++)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested) throw new TaskCanceledException();
                var resourceVerifier = new ResourceVerifier(_configurations);
                resourceVerifier.OnIdle += () => ResourceVerifierPool.Add(resourceVerifier);
                ResourceVerifierPool.Add(resourceVerifier);
            }
        }

        public delegate void ResourceVerifiedEvent(VerificationResult verificationResult);
        public delegate void StoppedEvent(bool isAllWorkDone = false);
        public delegate void WebBrowserClosedEvent(int closedWebBrowserCount);
        public delegate void WebBrowserOpenedEvent(int openedWebBrowserCount);
    }
}