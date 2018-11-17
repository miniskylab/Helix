using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Helix.Abstractions;

namespace Helix.Implementations
{
    public static class Crawler
    {
        static int _activeThreadCount;
        static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        static Configurations _configurations;
        static Task[] _crawlingTasks = { };
        static Task _mainWorkingTask;
        static int _verifiedUrlCount;
        static readonly ConcurrentDictionary<string, bool> AlreadyVerifiedUrls = new ConcurrentDictionary<string, bool>();
        static readonly string WorkingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        static readonly string ErrorFilePath = Path.Combine(WorkingDirectory, "errors.txt");
        static readonly BlockingCollection<IResourceCollector> ResourceCollectorPool = new BlockingCollection<IResourceCollector>();
        static readonly BlockingCollection<IResourceVerifier> ResourceVerifierPool = new BlockingCollection<IResourceVerifier>();
        static readonly object StaticLock = new object();
        static readonly BlockingCollection<IRawResource> ToBeVerifiedRawResources = new BlockingCollection<IRawResource>();

        public static CrawlerState State { get; private set; } = CrawlerState.Ready;
        public static CancellationToken CancellationToken => _cancellationTokenSource.Token;
        public static int RemainingUrlCount => _activeThreadCount;
        public static int VerifiedUrlCount => _verifiedUrlCount;

        public static event ExceptionOccurredEvent OnExceptionOccurred;
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

        public static void StartWorking()
        {
            lock (StaticLock)
            {
                if (State == CrawlerState.Unknown || State != CrawlerState.Ready) return;
                State = CrawlerState.Working;
            }

            _configurations = ServiceLocator.Get<Configurations>();
            _cancellationTokenSource = new CancellationTokenSource();
            _crawlingTasks = new Task[_configurations.WebBrowserCount];
            _activeThreadCount = 0;
            _mainWorkingTask = Task.Run(() =>
            {
                try
                {
                    while (ToBeVerifiedRawResources.Any()) ToBeVerifiedRawResources.Take();
                    lock (StaticLock)
                    {
                        AlreadyVerifiedUrls.Clear();
                        AlreadyVerifiedUrls.TryAdd(_configurations.StartUrl, true);
                        ToBeVerifiedRawResources.Add(new RawResource { Url = _configurations.StartUrl, ParentUrl = null });
                    }
                    InitializeResourceCollectorPool();
                    InitializeResourceVerifierPool();
                    Crawl();
                }
                catch (Exception exception)
                {
                    switch (exception)
                    {
                        case OperationCanceledException operationCanceledException:
                            if (operationCanceledException.CancellationToken.IsCancellationRequested) return;
                            break;
                    }
                    File.WriteAllText(ErrorFilePath, exception.ToString());
                    OnExceptionOccurred?.Invoke(exception);
                }
                finally { Task.Run(StopWorking); }
            }, _cancellationTokenSource.Token);
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

            var isAllWorkDone = !ToBeVerifiedRawResources.Any() && _activeThreadCount == 0;
            while (ResourceVerifierPool.Any()) ResourceVerifierPool.Take().Dispose();
            while (ResourceCollectorPool.Any())
            {
                ResourceCollectorPool.Take().Dispose();
                OnWebBrowserClosed?.Invoke(_configurations.WebBrowserCount - ResourceCollectorPool.Count);
            }
            ReportWriter.Instance.Dispose();
            ServiceLocator.Dispose();
            _activeThreadCount = 0;
            OnStopped?.Invoke(isAllWorkDone);
        }

        static void Crawl()
        {
            while (ToBeVerifiedRawResources.TryTake(out var rawResource) || _activeThreadCount > 0)
            {
                if (rawResource != null)
                {
                    var toBeVerifiedRawResource = rawResource;
                    Interlocked.Increment(ref _activeThreadCount);
                    Task.Run(() =>
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            Interlocked.Decrement(ref _activeThreadCount);
                            return;
                        }

                        var verificationResult = ResourceVerifierPool.Take(_cancellationTokenSource.Token).Verify(toBeVerifiedRawResource);
                        if (verificationResult.HttpStatusCode != (int) HttpStatusCode.ExpectationFailed)
                        {
                            ReportWriter.Instance.WriteReport(verificationResult, _configurations.ReportBrokenLinksOnly);
                            Interlocked.Increment(ref _verifiedUrlCount);
                            OnResourceVerified?.Invoke(verificationResult);
                        }

                        if (verificationResult.IsBrokenResource || !verificationResult.IsInternalResource)
                        {
                            Interlocked.Decrement(ref _activeThreadCount);
                            return;
                        }

                        if (toBeVerifiedRawResource.HttpStatusCode == 0)
                        {
                            var resource = verificationResult.Resource;
                            ResourceCollectorPool.Take(_cancellationTokenSource.Token).CollectNewRawResourcesFrom(resource);
                        }
                        Interlocked.Decrement(ref _activeThreadCount);
                    }, _cancellationTokenSource.Token);
                }

                Thread.Sleep(100);
                if (_cancellationTokenSource.Token.IsCancellationRequested) return;
            }
        }

        static void EnsureErrorLogFileIsRecreated()
        {
            if (File.Exists(ErrorFilePath)) File.Delete(ErrorFilePath);
        }

        static void InitializeResourceCollectorPool()
        {
            var openedWebBrowserCount = 0;
            Parallel.For(0, _configurations.WebBrowserCount, resourceCollectorId =>
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested) throw new TaskCanceledException();
                var resourceCollector = ServiceLocator.Get<IResourceCollector>();
                resourceCollector.OnRawResourceCollected += rawResource => Task.Run(() =>
                {
                    lock (StaticLock)
                    {
                        if (AlreadyVerifiedUrls.ContainsKey(rawResource.Url.StripFragment())) return;
                        AlreadyVerifiedUrls.TryAdd(rawResource.Url.StripFragment(), true);
                    }
                    ToBeVerifiedRawResources.Add(rawResource);
                });
                resourceCollector.OnBrowserExceptionOccurred += (exception, resource) =>
                {
                    /* TODO: How and Where do we log this information? */
                };
                resourceCollector.OnAllAttemptsToCollectNewRawResourcesFailed += parentResource =>
                {
                    /* TODO: How and Where do we log this information? */
                };
                resourceCollector.OnIdle += () => ResourceCollectorPool.Add(resourceCollector);
                ResourceCollectorPool.Add(resourceCollector);
                OnWebBrowserOpened?.Invoke(Interlocked.Increment(ref openedWebBrowserCount));
            });
        }

        static void InitializeResourceVerifierPool()
        {
            for (var resourceVerifierId = 0; resourceVerifierId < 10 * _configurations.WebBrowserCount; resourceVerifierId++)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested) throw new TaskCanceledException();
                var resourceVerifier = ServiceLocator.Get<IResourceVerifier>();
                resourceVerifier.OnIdle += () => ResourceVerifierPool.Add(resourceVerifier);
                ResourceVerifierPool.Add(resourceVerifier);
            }
        }

        public delegate void ExceptionOccurredEvent(Exception exception);
        public delegate Task ResourceVerifiedEvent(IVerificationResult verificationResult);
        public delegate void StoppedEvent(bool isAllWorkDone = false);
        public delegate void WebBrowserClosedEvent(int closedWebBrowserCount);
        public delegate void WebBrowserOpenedEvent(int openedWebBrowserCount);
    }
}