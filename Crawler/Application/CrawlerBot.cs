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
        public static IMemory Memory = ServiceLocator.Get<IMemory>();
        static FilePersistence _filePersistence;
        static readonly List<Task> BackgroundTasks;
        static readonly object ExtractionSyncRoot = new object();
        static readonly BlockingCollection<IRawResourceExtractor> RawResourceExtractorPool;
        static readonly object RenderingSyncRoot = new object();
        static readonly BlockingCollection<IResourceVerifier> ResourceVerifierPool;
        static readonly object VerificationSyncRoot = new object();
        static readonly BlockingCollection<IWebBrowser> WebBrowserPool;

        static bool EverythingIsDone
        {
            get
            {
                lock (ExtractionSyncRoot)
                lock (RenderingSyncRoot)
                lock (VerificationSyncRoot)
                {
                    var activeExtractionThreadCount = Memory.ActiveExtractionThreadCount;
                    var activeRenderingThreadCount = Memory.ActiveRenderingThreadCount;
                    var activeVerificationThreadCount = Memory.ActiveVerificationThreadCount;
                    var totalActiveThreadCount = activeExtractionThreadCount + activeRenderingThreadCount + activeVerificationThreadCount;
                    return Memory.NothingLeftToDo && totalActiveThreadCount == 0;
                }
            }
        }

        public static event ExceptionOccurredEvent OnExceptionOccurred;
        public static event ResourceVerifiedEvent OnResourceVerified;
        public static event StoppedEvent OnStopped;
        public static event WebBrowserClosedEvent OnWebBrowserClosed;
        public static event WebBrowserOpenedEvent OnWebBrowserOpened;

        static CrawlerBot()
        {
            BackgroundTasks = new List<Task>();
            RawResourceExtractorPool = new BlockingCollection<IRawResourceExtractor>();
            ResourceVerifierPool = new BlockingCollection<IResourceVerifier>();
            WebBrowserPool = new BlockingCollection<IWebBrowser>();
        }

        public static void Dispose()
        {
            StopWorking();
            WebBrowserPool?.Dispose();
            RawResourceExtractorPool?.Dispose();
            ResourceVerifierPool?.Dispose();
        }

        public static void StartWorking(Configurations configurations)
        {
            ServiceLocator.RegisterServices(configurations);
            Memory = ServiceLocator.Get<IMemory>();

            if (!Memory.TryTransitTo(CrawlerState.Working)) return;
            BackgroundTasks.Add(Task.Run(() =>
            {
                try
                {
                    EnsureErrorLogFileIsRecreated();
                    InitializeWebBrowserPool();
                    InitializeResourceVerifierPool();
                    InitializeRawResourceExtractorPool();

                    var renderingTask = Task.Run(Render, Memory.CancellationToken);
                    var extractionTask = Task.Run(Extract, Memory.CancellationToken);
                    var verificationTask = Task.Run(Verify, Memory.CancellationToken);
                    BackgroundTasks.Add(renderingTask);
                    BackgroundTasks.Add(extractionTask);
                    BackgroundTasks.Add(verificationTask);
                    Task.WhenAll(renderingTask, extractionTask, verificationTask).Wait();
                }
                catch (Exception exception) { HandleException(exception); }
                finally { Task.Run(StopWorking); }
            }, Memory.CancellationToken));
        }

        public static void StopWorking()
        {
            if (!Memory.TryTransitTo(CrawlerState.Stopping)) return;
            Memory.CancelEverything();
            Task.WhenAll(BackgroundTasks).Wait();
            while (ResourceVerifierPool.Any()) ResourceVerifierPool.Take().Dispose();
            while (RawResourceExtractorPool.Any()) RawResourceExtractorPool.Take();
            while (WebBrowserPool.Any())
            {
                WebBrowserPool.Take().Dispose();
                OnWebBrowserClosed?.Invoke(Memory.Configurations.WebBrowserCount - WebBrowserPool.Count);
            }
            ReportWriter.Instance.Dispose();
            ServiceLocator.Dispose();
            _filePersistence.Dispose();

            lock (ExtractionSyncRoot) Memory.ActiveExtractionThreadCount = 0;
            lock (RenderingSyncRoot) Memory.ActiveRenderingThreadCount = 0;
            lock (VerificationSyncRoot) Memory.ActiveVerificationThreadCount = 0;
            Memory.TryTransitTo(CrawlerState.Ready);
            OnStopped?.Invoke(EverythingIsDone);
        }

        static void EnsureErrorLogFileIsRecreated()
        {
            _filePersistence = new FilePersistence(Memory.ErrorFilePath);
            _filePersistence.WriteLineAsync(DateTime.Now.ToString(CultureInfo.InvariantCulture));
        }

        static void Extract()
        {
            while (!EverythingIsDone)
            {
                if (Memory.CancellationToken.IsCancellationRequested) return;
                while (true)
                {
                    Monitor.Enter(ExtractionSyncRoot);
                    if (Memory.ActiveExtractionThreadCount >= 300)
                    {
                        Monitor.Exit(ExtractionSyncRoot);
                        Thread.Sleep(TimeSpan.FromSeconds(3));
                        continue;
                    }
                    Memory.ActiveExtractionThreadCount++;
                    Monitor.Exit(ExtractionSyncRoot);
                    break;
                }

                HtmlDocument toBeExtractedHtmlDocument;
                IRawResourceExtractor rawResourceExtractor;
                try
                {
                    toBeExtractedHtmlDocument = Memory.TakeToBeExtractedHtmlDocument();
                    rawResourceExtractor = RawResourceExtractorPool.Take(Memory.CancellationToken);
                }
                catch (OperationCanceledException operationCanceledException)
                {
                    lock (ExtractionSyncRoot) Memory.ActiveExtractionThreadCount--;
                    if (operationCanceledException.CancellationToken != Memory.CancellationToken) throw;
                    return;
                }

                Task.Run(() =>
                {
                    rawResourceExtractor.ExtractRawResourcesFrom(toBeExtractedHtmlDocument, rawResource => Memory.Memorize(rawResource));
                    lock (ExtractionSyncRoot) Memory.ActiveExtractionThreadCount--;
                }, Memory.CancellationToken);
            }
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

        static void InitializeRawResourceExtractorPool()
        {
            const int rawResourceExtractorCount = 300;
            for (var rawResourceExtractorId = 0; rawResourceExtractorId < rawResourceExtractorCount; rawResourceExtractorId++)
            {
                if (Memory.CancellationToken.IsCancellationRequested) throw new OperationCanceledException(Memory.CancellationToken);
                var rawResourceExtractor = ServiceLocator.Get<IRawResourceExtractor>();
                rawResourceExtractor.OnIdle += () => RawResourceExtractorPool.Add(rawResourceExtractor);
                RawResourceExtractorPool.Add(rawResourceExtractor);
            }
        }

        static void InitializeResourceVerifierPool()
        {
            const int resourceVerifierCount = 2500;
            for (var resourceVerifierId = 0; resourceVerifierId < resourceVerifierCount; resourceVerifierId++)
            {
                if (Memory.CancellationToken.IsCancellationRequested) throw new OperationCanceledException(Memory.CancellationToken);
                var resourceVerifier = ServiceLocator.Get<IResourceVerifier>();
                resourceVerifier.OnIdle += () => ResourceVerifierPool.Add(resourceVerifier);
                ResourceVerifierPool.Add(resourceVerifier);
            }
        }

        static void InitializeWebBrowserPool()
        {
            var openedWebBrowserCount = 0;
            Parallel.For(0, Memory.Configurations.WebBrowserCount, webBrowserId =>
            {
                if (Memory.CancellationToken.IsCancellationRequested) throw new OperationCanceledException(Memory.CancellationToken);
                var webBrowser = ServiceLocator.Get<IWebBrowser>();
                webBrowser.OnIdle += () => WebBrowserPool.Add(webBrowser);
                webBrowser.OnRawResourceCaptured += rawResource => Memory.Memorize(rawResource);
                WebBrowserPool.Add(webBrowser);
                OnWebBrowserOpened?.Invoke(Interlocked.Increment(ref openedWebBrowserCount));
            });
        }

        static void Render()
        {
            while (!EverythingIsDone)
            {
                if (Memory.CancellationToken.IsCancellationRequested) return;
                while (true)
                {
                    Monitor.Enter(RenderingSyncRoot);
                    if (Memory.ActiveRenderingThreadCount >= 300)
                    {
                        Monitor.Exit(RenderingSyncRoot);
                        Thread.Sleep(TimeSpan.FromSeconds(3));
                        continue;
                    }
                    Memory.ActiveRenderingThreadCount++;
                    Monitor.Exit(RenderingSyncRoot);
                    break;
                }

                Uri toBeRenderedUri;
                IWebBrowser webBrowser;
                try
                {
                    toBeRenderedUri = Memory.TakeToBeRenderedUri();
                    webBrowser = WebBrowserPool.Take(Memory.CancellationToken);
                }
                catch (OperationCanceledException operationCanceledException)
                {
                    lock (RenderingSyncRoot) Memory.ActiveRenderingThreadCount--;
                    if (operationCanceledException.CancellationToken != Memory.CancellationToken) throw;
                    return;
                }

                Task.Run(() =>
                {
                    if (webBrowser.TryRender(toBeRenderedUri, exception => HandleException(exception), out var htmlText))
                        Memory.Memorize(new HtmlDocument
                        {
                            Uri = toBeRenderedUri,
                            Text = htmlText
                        });
                    lock (RenderingSyncRoot) Memory.ActiveRenderingThreadCount--;
                }, Memory.CancellationToken);
            }
        }

        static void Verify()
        {
            var resourceScope = ServiceLocator.Get<IResourceScope>();
            while (!EverythingIsDone)
            {
                if (Memory.CancellationToken.IsCancellationRequested) return;
                while (true)
                {
                    Monitor.Enter(VerificationSyncRoot);
                    if (Memory.ActiveVerificationThreadCount >= 400)
                    {
                        Monitor.Exit(VerificationSyncRoot);
                        Thread.Sleep(TimeSpan.FromSeconds(3));
                        continue;
                    }
                    Memory.ActiveVerificationThreadCount++;
                    Monitor.Exit(VerificationSyncRoot);
                    break;
                }

                RawResource toBeVerifiedRawResource;
                IResourceVerifier resourceVerifier;
                try
                {
                    toBeVerifiedRawResource = Memory.TakeToBeVerifiedRawResource();
                    resourceVerifier = ResourceVerifierPool.Take(Memory.CancellationToken);
                }
                catch (OperationCanceledException operationCanceledException)
                {
                    lock (VerificationSyncRoot) Memory.ActiveVerificationThreadCount--;
                    if (operationCanceledException.CancellationToken != Memory.CancellationToken) throw;
                    return;
                }

                Task.Run(() =>
                {
                    if (resourceVerifier.TryVerify(toBeVerifiedRawResource, out var verificationResult))
                    {
                        var isStartUrl = verificationResult.Resource != null && resourceScope.IsStartUri(verificationResult.Resource.Uri);
                        var isOrphanedUrl = verificationResult.RawResource.ParentUri == null;
                        if (isStartUrl || !isOrphanedUrl)
                        {
                            // TODO: Investigate where those orphaned Uri-s came from.
                            ReportWriter.Instance.WriteReport(verificationResult, Memory.Configurations.ReportBrokenLinksOnly);
                            OnResourceVerified?.Invoke(verificationResult);
                        }

                        var resourceExists = verificationResult.Resource != null;
                        var isExtracted = verificationResult.IsExtractedResource;
                        var isNotBroken = !verificationResult.IsBrokenResource;
                        var isInternal = verificationResult.IsInternalResource;
                        if (resourceExists && isExtracted && isNotBroken && isInternal) Memory.Memorize(verificationResult.Resource.Uri);
                    }
                    lock (VerificationSyncRoot) Memory.ActiveVerificationThreadCount--;
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