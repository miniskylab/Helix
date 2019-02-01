using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public static class CrawlerBot
    {
        static FilePersistence _filePersistence;
        static IManagement _management;
        static IMemory _memory;
        static readonly List<Task> BackgroundTasks;

        public static CancellationToken CancellationToken => _management?.CancellationToken ?? CancellationToken.None;

        public static CrawlerState CrawlerState => _management?.CrawlerState ?? CrawlerState.Ready;

        public static int RemainingUrlCount => _management?.RemainingUrlCount ?? 0;

        public static event Action<Exception> OnExceptionOccurred;
        public static event Func<VerificationResult, Task> OnResourceVerified;
        public static event Action<bool> OnStopped;

        static CrawlerBot() { BackgroundTasks = new List<Task>(); }

        public static void StartWorking(Configurations configurations)
        {
            ServiceLocator.RegisterServices(configurations);
            _management = ServiceLocator.Get<IManagement>();
            _memory = ServiceLocator.Get<IMemory>();

            if (!_management.TryTransitTo(CrawlerState.Working)) return;
            BackgroundTasks.Add(Task.Run(() =>
            {
                try
                {
                    EnsureErrorLogFileIsRecreated();
                    _management.EnsureResources();

                    var renderingTask = Task.Run(Render, _management.CancellationToken);
                    var extractionTask = Task.Run(Extract, _management.CancellationToken);
                    var verificationTask = Task.Run(Verify, _management.CancellationToken);
                    BackgroundTasks.Add(renderingTask);
                    BackgroundTasks.Add(extractionTask);
                    BackgroundTasks.Add(verificationTask);
                    Task.WhenAll(renderingTask, extractionTask, verificationTask).Wait();
                }
                catch (Exception exception) { HandleException(exception); }
                finally { Task.Run(StopWorking); }
            }, _management.CancellationToken));

            void EnsureErrorLogFileIsRecreated()
            {
                var errorLogFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "helix_errors.log");
                _filePersistence = new FilePersistence(errorLogFilePath);
                _filePersistence.WriteLineAsync(DateTime.Now.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static void StopWorking()
        {
            if (_management != null && !_management.TryTransitTo(CrawlerState.Stopping)) return;
            var everythingIsDone = _management?.EverythingIsDone ?? false;

            _management?.CancelEverything();
            try { Task.WhenAll(BackgroundTasks).Wait(); }
            catch (Exception exception) { HandleException(exception); }

            if (_management != null)
            {
                _management.OnOrphanedResourcesDetected += errorMessage => HandleException(new WarningException(errorMessage));
                _management.Dispose();
            }

            ReportWriter.Instance.Dispose();
            _filePersistence?.Dispose();
            ServiceLocator.Dispose();

            _management?.TryTransitTo(CrawlerState.Ready);
            OnStopped?.Invoke(everythingIsDone);
        }

        static void Extract()
        {
            while (!_management.EverythingIsDone && !_management.CancellationToken.IsCancellationRequested)
            {
                try
                {
                    _management.InterlockedCoordinate(out IRawResourceExtractor rawResourceExtractor, out var toBeExtractedHtmlDocument);
                    Task.Run(
                        () =>
                        {
                            try
                            {
                                rawResourceExtractor.ExtractRawResourcesFrom(
                                    toBeExtractedHtmlDocument,
                                    rawResource => _memory.Memorize(rawResource, _management.CancellationToken)
                                );
                            }
                            catch (OperationCanceledException operationCanceledException) { HandleException(operationCanceledException); }
                            finally { _management.OnRawResourceExtractionTaskCompleted(); }
                        },
                        _management.CancellationToken
                    ).ContinueWith(
                        _ => _management.OnRawResourceExtractionTaskCompleted(),
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
            while (!_management.EverythingIsDone && !_management.CancellationToken.IsCancellationRequested)
            {
                try
                {
                    _management.InterlockedCoordinate(out IWebBrowser webBrowser, out var toBeRenderedUri);
                    Task.Run(
                        () =>
                        {
                            try
                            {
                                void OnFailed(Exception exception) => HandleException(exception);
                                if (webBrowser.TryRender(toBeRenderedUri, OnFailed, _management.CancellationToken, out var htmlText))
                                    _memory.Memorize(new HtmlDocument
                                    {
                                        Uri = toBeRenderedUri,
                                        Text = htmlText
                                    }, _management.CancellationToken);
                            }
                            catch (OperationCanceledException operationCanceledException) { HandleException(operationCanceledException); }
                            finally { _management.OnUriRenderingTaskCompleted(); }
                        },
                        _management.CancellationToken
                    ).ContinueWith(
                        _ => _management.OnUriRenderingTaskCompleted(),
                        TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                    );
                }
                catch (Exception exception) { HandleException(exception); }
            }
        }

        static void Verify()
        {
            var resourceScope = ServiceLocator.Get<IResourceScope>();
            while (!_management.EverythingIsDone && !_management.CancellationToken.IsCancellationRequested)
            {
                try
                {
                    _management.InterlockedCoordinate(out IRawResourceVerifier rawResourceVerifier, out var toBeVerifiedRawResource);
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
                                    _memory.Memorize(verifiedResource.Uri, _management.CancellationToken);
                            }
                            catch (OperationCanceledException operationCanceledException) { HandleException(operationCanceledException); }
                            finally { _management.OnRawResourceVerificationTaskCompleted(); }
                        },
                        _management.CancellationToken
                    ).ContinueWith(
                        _ => _management.OnRawResourceVerificationTaskCompleted(),
                        TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                    );
                }
                catch (Exception exception) { HandleException(exception); }
            }
        }
    }
}