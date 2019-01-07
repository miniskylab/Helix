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
    public static class CrawlerBotV2
    {
        public static IMemory Memory = ServiceLocator.Get<IMemory>();
        static FilePersistence _filePersistence;
        static Task _mainWorkingTask;
        static readonly BlockingCollection<IHtmlParser> HtmlParserPool = new BlockingCollection<IHtmlParser>();
        static readonly BlockingCollection<IResourceVerifier> ResourceVerifierPool = new BlockingCollection<IResourceVerifier>();
        static readonly BlockingCollection<IWebBrowser> WebBrowserPool = new BlockingCollection<IWebBrowser>();

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
                    InitializeWebBrowserPool();
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

        static void InitializeHtmlParserPool()
        {
            var htmlParserCount = 30 * Memory.Configurations.WebBrowserCount;
            for (var htmlParserId = 0; htmlParserId < htmlParserCount; htmlParserId++)
            {
                if (Memory.CancellationToken.IsCancellationRequested) throw new OperationCanceledException(Memory.CancellationToken);
                var htmlParser = ServiceLocator.Get<IHtmlParser>();
                HtmlParserPool.Add(htmlParser);
            }
        }

        static void InitializeWebBrowserPool()
        {
            var openedWebBrowserCount = 0;
            Parallel.For(0, Memory.Configurations.WebBrowserCount, webBrowserId =>
            {
                if (Memory.CancellationToken.IsCancellationRequested) throw new OperationCanceledException(Memory.CancellationToken);
                WebBrowserPool.Add(ServiceLocator.Get<IWebBrowser>());
                OnWebBrowserOpened?.Invoke(Interlocked.Increment(ref openedWebBrowserCount));
            });
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