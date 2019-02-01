using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        public static event ExceptionOccurredEvent OnExceptionOccurred;
        public static event ResourceVerifiedEvent OnResourceVerified;
        public static event StoppedEvent OnStopped;

        static CrawlerBot() { BackgroundTasks = new List<Task>(); }

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
                    Management.EnsureResources();

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
            var everythingIsDone = Management.EverythingIsDone;
            Management.CancelEverything();
            try { Task.WhenAll(BackgroundTasks).Wait(); }
            catch (Exception exception) { HandleException(exception); }
            ReportWriter.Instance.Dispose();
            Management.Dispose();
            ServiceLocator.Dispose();
            _filePersistence.Dispose();

            Management.TryTransitTo(CrawlerState.Ready);
            OnStopped?.Invoke(everythingIsDone);
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
                try
                {
                    Management.InterlockedCoordinate(out IRawResourceExtractor rawResourceExtractor, out var toBeExtractedHtmlDocument);
                    Task.Run(
                        () =>
                        {
                            try
                            {
                                rawResourceExtractor.ExtractRawResourcesFrom(
                                    toBeExtractedHtmlDocument,
                                    rawResource => _memory.Memorize(rawResource, Management.CancellationToken)
                                );
                            }
                            catch (OperationCanceledException operationCanceledException) { HandleException(operationCanceledException); }
                            finally { Management.OnRawResourceExtractionTaskCompleted(); }
                        },
                        Management.CancellationToken
                    ).ContinueWith(
                        _ => Management.OnRawResourceExtractionTaskCompleted(),
                        TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                    );
                }
                catch (Exception exception) { HandleException(exception); }
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

        static void Render()
        {
            while (!Management.EverythingIsDone && !Management.CancellationToken.IsCancellationRequested)
            {
                try
                {
                    Management.InterlockedCoordinate(out IWebBrowser webBrowser, out var toBeRenderedUri);
                    Task.Run(
                        () =>
                        {
                            try
                            {
                                void OnFailed(Exception exception) => HandleException(exception);
                                if (webBrowser.TryRender(toBeRenderedUri, OnFailed, Management.CancellationToken, out var htmlText))
                                    _memory.Memorize(new HtmlDocument
                                    {
                                        Uri = toBeRenderedUri,
                                        Text = htmlText
                                    }, Management.CancellationToken);
                            }
                            catch (OperationCanceledException operationCanceledException) { HandleException(operationCanceledException); }
                            finally { Management.OnUriRenderingTaskCompleted(); }
                        },
                        Management.CancellationToken
                    ).ContinueWith(
                        _ => Management.OnUriRenderingTaskCompleted(),
                        TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                    );
                }
                catch (Exception exception) { HandleException(exception); }
            }
        }

        static void Verify()
        {
            var resourceScope = ServiceLocator.Get<IResourceScope>();
            while (!Management.EverythingIsDone && !Management.CancellationToken.IsCancellationRequested)
            {
                try
                {
                    Management.InterlockedCoordinate(out IRawResourceVerifier rawResourceVerifier, out var toBeVerifiedRawResource);
                    Task.Run(
                        () =>
                        {
                            try
                            {
                                if (!rawResourceVerifier.TryVerify(toBeVerifiedRawResource, out var verificationResult)) return;
                                var verifiedResource = verificationResult.Resource;
                                var isStartUrl = verifiedResource != null && resourceScope.IsStartUri(verifiedResource.Uri);
                                var isOrphanedUrl = verificationResult.RawResource.ParentUri == null;
                                if (isStartUrl || !isOrphanedUrl)
                                {
                                    // TODO: Investigate where those orphaned Uri-s came from.
                                    ReportWriter.Instance.WriteReport(verificationResult, _memory.Configurations.ReportBrokenLinksOnly);
                                    OnResourceVerified?.Invoke(verificationResult);
                                }

                                var resourceExists = verifiedResource != null;
                                var isExtracted = verificationResult.IsExtractedResource;
                                var isNotBroken = !verificationResult.IsBrokenResource;
                                var isInternal = verificationResult.IsInternalResource;
                                if (resourceExists && isExtracted && isNotBroken && isInternal)
                                    _memory.Memorize(verifiedResource.Uri, Management.CancellationToken);
                            }
                            catch (OperationCanceledException operationCanceledException) { HandleException(operationCanceledException); }
                            finally { Management.OnRawResourceVerificationTaskCompleted(); }
                        },
                        Management.CancellationToken
                    ).ContinueWith(
                        _ => Management.OnRawResourceVerificationTaskCompleted(),
                        TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                    );
                }
                catch (Exception exception) { HandleException(exception); }
            }
        }

        // TODO: Clean-up
        public delegate void ExceptionOccurredEvent(Exception exception);
        public delegate Task ResourceVerifiedEvent(VerificationResult verificationResult);
        public delegate void StoppedEvent(bool isAllWorkDone = false);
    }
}