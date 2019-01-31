using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public static class CrawlerBot
    {
        public static IManagement Management;
        static FilePersistence _filePersistence;
        static IMemory _memory;
        static readonly List<Task> BackgroundTasks;
        static readonly BlockingCollection<IRawResourceExtractor> RawResourceExtractorPool;
        static readonly BlockingCollection<IRawResourceVerifier> RawResourceVerifierPool;
        static readonly BlockingCollection<IWebBrowser> WebBrowserPool;

        public static event ExceptionOccurredEvent OnExceptionOccurred;
        public static event ResourceVerifiedEvent OnResourceVerified;
        public static event StoppedEvent OnStopped;
        public static event WebBrowserClosedEvent OnWebBrowserClosed;
        public static event WebBrowserOpenedEvent OnWebBrowserOpened;

        static CrawlerBot()
        {
            BackgroundTasks = new List<Task>();
            RawResourceExtractorPool = new BlockingCollection<IRawResourceExtractor>();
            RawResourceVerifierPool = new BlockingCollection<IRawResourceVerifier>();
            WebBrowserPool = new BlockingCollection<IWebBrowser>();
        }

        public static void Dispose()
        {
            StopWorking();
            WebBrowserPool?.Dispose();
            RawResourceExtractorPool?.Dispose();
            RawResourceVerifierPool?.Dispose();
        }

        public static void StartWorking(Configurations configurations)
        {
            ServiceLocator.RegisterServices(configurations);
            Management = ServiceLocator.Get<IManagement>();
            _memory = ServiceLocator.Get<IMemory>();

            if (!Management.TryTransitTo(CrawlerState.Working)) return;
            BackgroundTasks.Add(Task.Run(() =>
            {
                try
                {
                    EnsureErrorLogFileIsRecreated();
                    InitializeWebBrowserPool();
                    InitializeRawResourceVerifierPool();
                    InitializeRawResourceExtractorPool();

                    var renderingTask = Task.Run(Render, Management.CancellationToken);
                    var extractionTask = Task.Run(Extract, Management.CancellationToken);
                    var verificationTask = Task.Run(Verify, Management.CancellationToken);
                    BackgroundTasks.Add(renderingTask);
                    BackgroundTasks.Add(extractionTask);
                    BackgroundTasks.Add(verificationTask);
                    Task.WhenAll(renderingTask, extractionTask, verificationTask).Wait();
                }
                catch (Exception exception) { HandleException(exception); }
                finally { Task.Run(StopWorking); }
            }, Management.CancellationToken));
        }

        public static void StopWorking()
        {
            if (!Management.TryTransitTo(CrawlerState.Stopping)) return;
            Management.CancelEverything();
            try { Task.WhenAll(BackgroundTasks).Wait(); }
            catch (Exception exception) { HandleException(exception); }
            while (RawResourceVerifierPool.Any()) RawResourceVerifierPool.Take().Dispose();
            while (RawResourceExtractorPool.Any()) RawResourceExtractorPool.Take();
            while (WebBrowserPool.Any())
            {
                WebBrowserPool.Take().Dispose();
                OnWebBrowserClosed?.Invoke(_memory.Configurations.WebBrowserCount - WebBrowserPool.Count);
            }
            ReportWriter.Instance.Dispose();
            ServiceLocator.Dispose();
            _filePersistence.Dispose();

            Management.TryTransitTo(CrawlerState.Ready);
            OnStopped?.Invoke(Management.EverythingIsDone);
        }

        static void EnsureErrorLogFileIsRecreated()
        {
            _filePersistence = new FilePersistence(_memory.ErrorLogFilePath);
            _filePersistence.WriteLineAsync(DateTime.Now.ToString(CultureInfo.InvariantCulture));
        }

        static void Extract()
        {
            while (!Management.EverythingIsDone && !Management.CancellationToken.IsCancellationRequested)
            {
                HtmlDocument toBeExtractedHtmlDocument;
                IRawResourceExtractor rawResourceExtractor;
                try
                {
                    toBeExtractedHtmlDocument = Management.InterlockedTakeToBeExtractedHtmlDocument();
                    rawResourceExtractor = RawResourceExtractorPool.Take(Management.CancellationToken);
                }
                catch (Exception exception)
                {
                    HandleException(exception);
                    continue;
                }

                Task.Run(() =>
                    {
                        rawResourceExtractor.ExtractRawResourcesFrom(
                            toBeExtractedHtmlDocument,
                            rawResource => _memory.Memorize(rawResource, Management.CancellationToken)
                        );
                    },
                    Management.CancellationToken
                ).ContinueWith(_ => Management.OnRawResourceExtractionTaskCompleted(), TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        static void HandleException(Exception exception)
        {
            switch (exception)
            {
                case OperationCanceledException operationCanceledException:
                    if (operationCanceledException.CancellationToken.IsCancellationRequested) return;
                    break;
                case AggregateException aggregateException:
                    var thereIsNoUnhandledInnerException = !aggregateException.InnerExceptions.Any(innerException =>
                    {
                        if (!(innerException is OperationCanceledException operationCanceledException)) return false;
                        return !operationCanceledException.CancellationToken.IsCancellationRequested;
                    });
                    if (thereIsNoUnhandledInnerException) return;
                    break;
            }
            _filePersistence.WriteLineAsync(exception.ToString());
            OnExceptionOccurred?.Invoke(exception);
        }

        static void InitializeRawResourceExtractorPool()
        {
            const int rawResourceExtractorCount = 300;
            for (var rawResourceExtractorId = 0; rawResourceExtractorId < rawResourceExtractorCount; rawResourceExtractorId++)
            {
                if (Management.CancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(Management.CancellationToken);

                var rawResourceExtractor = ServiceLocator.Get<IRawResourceExtractor>();
                rawResourceExtractor.OnIdle += () => RawResourceExtractorPool.Add(rawResourceExtractor);
                RawResourceExtractorPool.Add(rawResourceExtractor);
            }
        }

        static void InitializeRawResourceVerifierPool()
        {
            const int rawResourceVerifierCount = 2500;
            for (var rawResourceVerifierId = 0; rawResourceVerifierId < rawResourceVerifierCount; rawResourceVerifierId++)
            {
                if (Management.CancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(Management.CancellationToken);

                var rawResourceVerifier = ServiceLocator.Get<IRawResourceVerifier>();
                rawResourceVerifier.OnIdle += () => RawResourceVerifierPool.Add(rawResourceVerifier);
                RawResourceVerifierPool.Add(rawResourceVerifier);
            }
        }

        static void InitializeWebBrowserPool()
        {
            var openedWebBrowserCount = 0;
            Parallel.For(0, _memory.Configurations.WebBrowserCount, webBrowserId =>
            {
                if (Management.CancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(Management.CancellationToken);

                var webBrowser = ServiceLocator.Get<IWebBrowser>();
                webBrowser.OnIdle += () => WebBrowserPool.Add(webBrowser);
                webBrowser.OnRawResourceCaptured += rawResource => _memory.Memorize(rawResource, Management.CancellationToken);
                WebBrowserPool.Add(webBrowser);
                OnWebBrowserOpened?.Invoke(Interlocked.Increment(ref openedWebBrowserCount));
            });
        }

        static void Render()
        {
            while (!Management.EverythingIsDone && !Management.CancellationToken.IsCancellationRequested)
            {
                Uri toBeRenderedUri;
                IWebBrowser webBrowser;
                try
                {
                    toBeRenderedUri = Management.InterlockedTakeToBeRenderedUri();
                    webBrowser = WebBrowserPool.Take(Management.CancellationToken);
                }
                catch (Exception exception)
                {
                    HandleException(exception);
                    continue;
                }

                Task.Run(() =>
                    {
                        void OnFailed(Exception exception) => HandleException(exception);
                        if (webBrowser.TryRender(toBeRenderedUri, OnFailed, Management.CancellationToken, out var htmlText))
                            _memory.Memorize(new HtmlDocument
                            {
                                Uri = toBeRenderedUri,
                                Text = htmlText
                            }, Management.CancellationToken);
                    },
                    Management.CancellationToken
                ).ContinueWith(_ => Management.OnUriRenderingTaskCompleted(), TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        static void Verify()
        {
            var resourceScope = ServiceLocator.Get<IResourceScope>();
            while (!Management.EverythingIsDone && !Management.CancellationToken.IsCancellationRequested)
            {
                RawResource toBeVerifiedRawResource;
                IRawResourceVerifier rawResourceVerifier;
                try
                {
                    toBeVerifiedRawResource = Management.InterlockedTakeToBeVerifiedRawResource();
                    rawResourceVerifier = RawResourceVerifierPool.Take(Management.CancellationToken);
                }
                catch (Exception exception)
                {
                    HandleException(exception);
                    continue;
                }

                Task.Run(() =>
                    {
                        if (!rawResourceVerifier.TryVerify(toBeVerifiedRawResource, out var verificationResult)) return;
                        var isStartUrl = verificationResult.Resource != null && resourceScope.IsStartUri(verificationResult.Resource.Uri);
                        var isOrphanedUrl = verificationResult.RawResource.ParentUri == null;
                        if (isStartUrl || !isOrphanedUrl)
                        {
                            // TODO: Investigate where those orphaned Uri-s came from.
                            ReportWriter.Instance.WriteReport(verificationResult, _memory.Configurations.ReportBrokenLinksOnly);
                            OnResourceVerified?.Invoke(verificationResult);
                        }

                        var resourceExists = verificationResult.Resource != null;
                        var isExtracted = verificationResult.IsExtractedResource;
                        var isNotBroken = !verificationResult.IsBrokenResource;
                        var isInternal = verificationResult.IsInternalResource;
                        if (resourceExists && isExtracted && isNotBroken && isInternal)
                            _memory.Memorize(verificationResult.Resource.Uri, Management.CancellationToken);
                    },
                    Management.CancellationToken
                ).ContinueWith(_ => Management.OnRawResourceVerificationTaskCompleted(), TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        public delegate void ExceptionOccurredEvent(Exception exception);
        public delegate Task ResourceVerifiedEvent(VerificationResult verificationResult);
        public delegate void StoppedEvent(bool isAllWorkDone = false);
        public delegate void WebBrowserClosedEvent(int closedWebBrowserCount);
        public delegate void WebBrowserOpenedEvent(int openedWebBrowserCount);
    }
}