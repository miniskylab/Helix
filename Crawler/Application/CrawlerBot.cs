using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public static class CrawlerBot
    {
        public static IMemory Memory = ServiceLocator.Get<IMemory>();
        static FilePersistence _filePersistence;
        static Task _mainWorkingTask;
        static readonly BlockingCollection<IResourceCollector> ResourceCollectorPool = new BlockingCollection<IResourceCollector>();
        static readonly BlockingCollection<IResourceVerifier> ResourceVerifierPool = new BlockingCollection<IResourceVerifier>();

        public static event ExceptionOccurredEvent OnExceptionOccurred;
        public static event ResourceVerifiedEvent OnResourceVerified;
        public static event StoppedEvent OnStopped;
        public static event WebBrowserClosedEvent OnWebBrowserClosed;
        public static event WebBrowserOpenedEvent OnWebBrowserOpened;

        public static void Dispose()
        {
            StopWorking();
            ResourceCollectorPool?.Dispose();
            ResourceVerifierPool?.Dispose();
        }

        public static void StartWorking(Configurations configurations)
        {
            ServiceLocator.RegisterServices(configurations);
            Memory = ServiceLocator.Get<IMemory>();
            if (!Memory.TryTransitTo(CrawlerState.Working)) return;
            _mainWorkingTask = Task.Run(() =>
            {
                try
                {
                    EnsureErrorLogFileIsRecreated();
                    InitializeResourceCollectorPool();
                    InitializeResourceVerifierPool();
                    Task.WhenAll(Task.Run(Verify, Memory.CancellationToken), Task.Run(Crawl, Memory.CancellationToken)).Wait();
                }
                catch (Exception exception) { HandleException(exception); }
                finally { Task.Run(StopWorking); }
            }, Memory.CancellationToken);
        }

        public static void StopWorking()
        {
            if (!Memory.TryTransitTo(CrawlerState.Stopping)) return;
            Memory.CancelEverything();
            _mainWorkingTask.Wait();
            while (ResourceVerifierPool.Any()) ResourceVerifierPool.Take().Dispose();
            while (ResourceCollectorPool.Any())
            {
                ResourceCollectorPool.Take().Dispose();
                OnWebBrowserClosed?.Invoke(Memory.Configurations.WebBrowserCount - ResourceCollectorPool.Count);
            }
            ReportWriter.Instance.Dispose();
            ServiceLocator.Dispose();
            _filePersistence.Dispose();
            Memory.TryTransitTo(CrawlerState.Ready);
            OnStopped?.Invoke(Memory.EverythingIsDone);
        }

        static void Crawl()
        {
            while (!Memory.EverythingIsDone)
            {
                if (Memory.CancellationToken.IsCancellationRequested) break;
                while (Memory.RemainingUrlCount > 1000) Thread.Sleep(3000);
                Memory.IncrementActiveThreadCount();
                if (!Memory.TryTakeToBeCrawledResource(out var toBeCrawledResource))
                {
                    Memory.DecrementActiveThreadCount();
                    Thread.Sleep(100);
                    continue;
                }

                Task.Run(() =>
                {
                    try { ResourceCollectorPool.Take(Memory.CancellationToken).CollectNewRawResourcesFrom(toBeCrawledResource); }
                    catch (Exception exception) { HandleException(exception); }
                    finally { Memory.DecrementActiveThreadCount(); }
                }, Memory.CancellationToken);
            }
        }

        static void EnsureErrorLogFileIsRecreated()
        {
            _filePersistence = new FilePersistence(Memory.ErrorFilePath);
            _filePersistence.WriteLineAsync(DateTime.Now.ToString(CultureInfo.InvariantCulture));
        }

        static void HandleException(Exception exception, string additionalTextMessage = "")
        {
            switch (exception)
            {
                case OperationCanceledException operationCanceledException:
                    if (operationCanceledException.CancellationToken.IsCancellationRequested) return;
                    break;
                case AggregateException aggregateException:
                    var thereIsNoUnhandledInnerException = !aggregateException.InnerExceptions.Any(innerException =>
                    {
                        if (!(innerException is OperationCanceledException operationCanceledException)) return true;
                        return !operationCanceledException.CancellationToken.IsCancellationRequested;
                    });
                    if (thereIsNoUnhandledInnerException) return;
                    break;
            }
            _filePersistence.WriteLineAsync($"{exception}{additionalTextMessage}");
            OnExceptionOccurred?.Invoke(exception);
        }

        static void InitializeResourceCollectorPool()
        {
            var openedWebBrowserCount = 0;
            Parallel.For(0, Memory.Configurations.WebBrowserCount, resourceCollectorId =>
            {
                if (Memory.CancellationToken.IsCancellationRequested) throw new OperationCanceledException(Memory.CancellationToken);
                var resourceCollector = ServiceLocator.Get<IResourceCollector>();
                resourceCollector.OnRawResourceCollected += rawResource => Task.Run(() => { Memory.Memorize(rawResource); });
                resourceCollector.OnBrowserExceptionOccurred += (exception, errorResource) =>
                {
                    if (Memory.CrawlerState == CrawlerState.Stopping || Memory.CrawlerState == CrawlerState.Ready) return;
                    HandleException(exception, $"\nError Resource:\nParent URL: {errorResource.ParentUri}\nURL: {errorResource.Uri}");
                };
                resourceCollector.OnAllAttemptsToCollectNewRawResourcesFailed += parentResource =>
                {
                    if (Memory.CrawlerState == CrawlerState.Stopping || Memory.CrawlerState == CrawlerState.Ready) return;
                    _filePersistence.WriteLineAsync($"\nAll attempts to collect new raw resources failed for URL: {parentResource.Uri}");
                };
                resourceCollector.OnIdle += () => ResourceCollectorPool.Add(resourceCollector);
                ResourceCollectorPool.Add(resourceCollector);
                OnWebBrowserOpened?.Invoke(Interlocked.Increment(ref openedWebBrowserCount));
            });
        }

        static void InitializeResourceVerifierPool()
        {
            for (var resourceVerifierId = 0; resourceVerifierId < 30 * Memory.Configurations.WebBrowserCount; resourceVerifierId++)
            {
                if (Memory.CancellationToken.IsCancellationRequested) throw new OperationCanceledException(Memory.CancellationToken);
                var resourceVerifier = ServiceLocator.Get<IResourceVerifier>();
                resourceVerifier.OnIdle += () => ResourceVerifierPool.Add(resourceVerifier);
                ResourceVerifierPool.Add(resourceVerifier);
            }
        }

        static void Verify()
        {
            while (!Memory.EverythingIsDone)
            {
                if (Memory.CancellationToken.IsCancellationRequested) break;
                Memory.IncrementActiveThreadCount();
                if (!Memory.TryTakeToBeVerifiedRawResource(out var toBeVerifiedRawResource))
                {
                    Memory.DecrementActiveThreadCount();
                    Thread.Sleep(100);
                    continue;
                }

                Task.Run(() =>
                {
                    try
                    {
                        if (Memory.CancellationToken.IsCancellationRequested) return;
                        var verificationResult = ResourceVerifierPool.Take(Memory.CancellationToken).Verify(toBeVerifiedRawResource);
                        if (verificationResult.HttpStatusCode != (int) HttpStatusCode.ExpectationFailed)
                        {
                            ReportWriter.Instance.WriteReport(verificationResult, Memory.Configurations.ReportBrokenLinksOnly);
                            OnResourceVerified?.Invoke(verificationResult);
                        }

                        var resourceIsCrawlable = toBeVerifiedRawResource.HttpStatusCode == 0;
                        if (!verificationResult.IsBrokenResource && verificationResult.IsInternalResource && resourceIsCrawlable)
                            Memory.Memorize(verificationResult.Resource);
                    }
                    catch (Exception exception) { HandleException(exception); }
                    finally { Memory.DecrementActiveThreadCount(); }
                }, Memory.CancellationToken);
            }
        }

        public delegate void ExceptionOccurredEvent(Exception exception);
        public delegate Task ResourceVerifiedEvent(VerificationResult verificationResult);
        public delegate void StoppedEvent(bool isAllWorkDone = false);
        public delegate void WebBrowserClosedEvent(int closedWebBrowserCount);
        public delegate void WebBrowserOpenedEvent(int openedWebBrowserCount);
    }
}