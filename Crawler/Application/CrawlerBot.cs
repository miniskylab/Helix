using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Crawler
{
    public static class CrawlerBot
    {
        static ILogger _logger;
        static IManagement _management;
        static IMemory _memory;
        static IReportWriter _reportWriter;
        static readonly List<Task> BackgroundTasks;

        public static Statistics Statistics { get; }

        public static CrawlerState CrawlerState => _management?.CrawlerState ?? CrawlerState.Ready;

        public static int RemainingUrlCount => _management?.RemainingUrlCount ?? 0;

        public static event Action<VerificationResult> OnResourceVerified;
        public static event Action<bool> OnStopped;

        static CrawlerBot()
        {
            Statistics = new Statistics();
            BackgroundTasks = new List<Task>();
        }

        public static void StartWorking(Configurations configurations)
        {
            ServiceLocator.RegisterServices(configurations);
            _management = ServiceLocator.Get<IManagement>();
            _memory = ServiceLocator.Get<IMemory>();
            _reportWriter = ServiceLocator.Get<IReportWriter>();

            if (!_management.TryTransitTo(CrawlerState.Working)) return;
            BackgroundTasks.Add(Task.Run(() =>
            {
                try
                {
                    EnsureErrorLogFileIsRecreated();
                    _management.EnsureEnoughResources();

                    var renderingTask = Task.Run(Render, _management.CancellationToken);
                    var extractionTask = Task.Run(Extract, _management.CancellationToken);
                    var verificationTask = Task.Run(Verify, _management.CancellationToken);
                    BackgroundTasks.Add(renderingTask);
                    BackgroundTasks.Add(extractionTask);
                    BackgroundTasks.Add(verificationTask);
                    Task.WhenAll(renderingTask, extractionTask, verificationTask).Wait();
                }
                catch (Exception exception) { _logger.LogException(exception); }
                finally { Task.Run(StopWorking); }
            }, _management.CancellationToken));

            void EnsureErrorLogFileIsRecreated()
            {
                _logger = ServiceLocator.Get<ILogger>();
                _logger.LogInfo("Started working ...");
            }
        }

        public static void StopWorking()
        {
            if (_management != null && !_management.TryTransitTo(CrawlerState.Stopping)) return;
            var everythingIsDone = _management?.EverythingIsDone ?? false;

            _management?.CancelEverything();
            try { Task.WhenAll(BackgroundTasks).Wait(); }
            catch (Exception exception) { _logger.LogException(exception); }

            _management?.Dispose();
            _reportWriter?.Dispose();
            _logger?.Dispose();
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
                            catch (OperationCanceledException operationCanceledException)
                            {
                                _logger.LogException(operationCanceledException);
                            }
                            finally { _management.OnRawResourceExtractionTaskCompleted(); }
                        },
                        _management.CancellationToken
                    ).ContinueWith(
                        _ => _management.OnRawResourceExtractionTaskCompleted(rawResourceExtractor, toBeExtractedHtmlDocument),
                        TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                    );
                }
                catch (Management.EverythingIsDoneException) { }
                catch (Exception exception) { _logger.LogException(exception); }
            }
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
                                Action<Exception> onFailed = _logger.LogException;
                                if (!webBrowser.TryRender(toBeRenderedUri, onFailed, _management.CancellationToken, out var htmlText,
                                    out var pageLoadTime)) return;

                                if (pageLoadTime.HasValue)
                                {
                                    Statistics.SuccessfullyRenderedPageCount++;
                                    Statistics.TotalPageLoadTime += pageLoadTime.Value;
                                }
                                else
                                {
                                    try { throw new InvalidConstraintException(); }
                                    catch (InvalidConstraintException invalidConstraintException)
                                    {
                                        _logger.LogException(invalidConstraintException);
                                    }
                                }

                                _memory.Memorize(new HtmlDocument
                                {
                                    Uri = toBeRenderedUri,
                                    Text = htmlText
                                }, _management.CancellationToken);
                            }
                            catch (OperationCanceledException operationCanceledException)
                            {
                                _logger.LogException(operationCanceledException);
                            }
                            finally { _management.OnUriRenderingTaskCompleted(); }
                        },
                        _management.CancellationToken
                    ).ContinueWith(
                        _ => _management.OnUriRenderingTaskCompleted(webBrowser, toBeRenderedUri),
                        TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                    );
                }
                catch (Management.EverythingIsDoneException) { }
                catch (Exception exception) { _logger.LogException(exception); }
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
                                    _reportWriter.WriteReport(verificationResult, _memory.Configurations.ReportBrokenLinksOnly);
                                    Statistics.VerifiedUrlCount++;
                                    if (verificationResult.IsBrokenResource) Statistics.BrokenUrlCount++;
                                    else Statistics.ValidUrlCount++;
                                    OnResourceVerified?.Invoke(verificationResult);
                                }

                                var resourceExists = verifiedResource != null;
                                var isExtracted = verificationResult.IsExtractedResource;
                                var isNotBroken = !verificationResult.IsBrokenResource;
                                var isInternal = verificationResult.IsInternalResource;
                                if (resourceExists && isExtracted && isNotBroken && isInternal)
                                    _memory.Memorize(verifiedResource.Uri, _management.CancellationToken);
                            }
                            catch (OperationCanceledException operationCanceledException)
                            {
                                _logger.LogException(operationCanceledException);
                            }
                            finally { _management.OnRawResourceVerificationTaskCompleted(); }
                        },
                        _management.CancellationToken
                    ).ContinueWith(
                        _ => _management.OnRawResourceVerificationTaskCompleted(rawResourceVerifier, toBeVerifiedRawResource),
                        TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                    );
                }
                catch (Management.EverythingIsDoneException) { }
                catch (Exception exception) { _logger.LogException(exception); }
            }
        }
    }
}