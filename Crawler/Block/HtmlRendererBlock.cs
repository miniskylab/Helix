using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Crawler.Abstractions;
using log4net;
using Microsoft.Extensions.Logging;

namespace Helix.Crawler
{
    public class HtmlRendererBlock : TransformBlock<Resource, RenderingResult>, IHtmlRendererBlock, IDisposable
    {
        readonly CancellationToken _cancellationToken;
        readonly BlockingCollection<IHtmlRenderer> _htmlRenderers;
        readonly ILog _log;
        readonly IStatistics _statistics;

        public BufferBlock<Event> Events { get; }

        public BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        public BufferBlock<VerificationResult> VerificationResults { get; }

        public override Task Completion => Task.WhenAll(
            base.Completion,
            Events.Completion,
            VerificationResults.Completion,
            FailedProcessingResults.Completion
        );

        public HtmlRendererBlock(CancellationToken cancellationToken, IStatistics statistics, ILog log, Configurations configurations,
            IHardwareMonitor hardwareMonitor, Func<IHtmlRenderer> getHtmlRenderer) : base(cancellationToken,
            maxDegreeOfParallelism: Environment.ProcessorCount)
        {
            _log = log;
            _statistics = statistics;
            _cancellationToken = cancellationToken;
            _htmlRenderers = new BlockingCollection<IHtmlRenderer>();
            (int createdHtmlRenderCount, int disposedHtmlRendererCount) counter = (0, 0);

            var dataflowBlockOptions = new DataflowBlockOptions { CancellationToken = cancellationToken };
            VerificationResults = new BufferBlock<VerificationResult>(dataflowBlockOptions);
            FailedProcessingResults = new BufferBlock<FailedProcessingResult>(dataflowBlockOptions);
            Events = new BufferBlock<Event>(new DataflowBlockOptions { EnsureOrdered = true, CancellationToken = cancellationToken });

            base.Completion.ContinueWith(_ =>
            {
                FailedProcessingResults.Complete();
                VerificationResults.Complete();
                DisposeHtmlRenderers();
                Events.Complete();
                CheckMemoryLeak();

                #region Local Functions

                void CheckMemoryLeak()
                {
                    if (counter.disposedHtmlRendererCount == counter.createdHtmlRenderCount) return;
                    var resourceName = $"{nameof(HtmlRenderer)}{(counter.createdHtmlRenderCount > 1 ? "s" : string.Empty)}";
                    var disposedCountText = counter.disposedHtmlRendererCount == 0 ? "none" : $"only {counter.disposedHtmlRendererCount}";
                    _log.Warn(
                        "Orphaned resources detected! " +
                        $"{counter.createdHtmlRenderCount} {resourceName} were created but {disposedCountText} could be found and disposed."
                    );
                }
                void DisposeHtmlRenderers()
                {
                    while (_htmlRenderers.Any())
                    {
                        _htmlRenderers.Take().Dispose();
                        counter.disposedHtmlRendererCount++;

                        var webBrowserClosedEvent = new Event
                        {
                            EventType = EventType.StopProgressUpdated,
                            Message = $"Closing web browsers ({counter.disposedHtmlRendererCount}/{counter.createdHtmlRenderCount}) ..."
                        };
                        if (!Events.Post(webBrowserClosedEvent) && !Events.Completion.IsCompleted)
                            _log.Error($"Failed to post data to buffer block named [{nameof(Events)}].");
                    }
                    _htmlRenderers?.Dispose();
                }

                #endregion Local Functions
            });

            CreateHtmlRenderer();
            CreateAndDestroyHtmlRenderersAdaptively();

            #region Local Functions

            void CreateHtmlRenderer()
            {
                _htmlRenderers.Add(getHtmlRenderer(), CancellationToken.None);
                counter.createdHtmlRenderCount++;
            }
            void CreateAndDestroyHtmlRenderersAdaptively()
            {
                hardwareMonitor.OnLowCpuAndMemoryUsage += (averageCpuUsage, memoryUsage) =>
                {
                    if (_htmlRenderers.Count > 0 || counter.createdHtmlRenderCount == configurations.MaxHtmlRendererCount) return;
                    CreateHtmlRenderer();

                    _log.Info(
                        $"Low CPU usage ({averageCpuUsage}%) and low memory usage ({memoryUsage}%) detected. " +
                        $"Browser count increased from {counter.createdHtmlRenderCount - 1} to {counter.createdHtmlRenderCount}."
                    );
                };
                hardwareMonitor.OnHighCpuOrMemoryUsage += (averageCpuUsage, memoryUsage) =>
                {
                    if (counter.createdHtmlRenderCount == 1) return;
                    _htmlRenderers.Take().Dispose();
                    counter.createdHtmlRenderCount--;

                    if (averageCpuUsage == null && memoryUsage == null)
                        throw new ArgumentException(nameof(averageCpuUsage), nameof(memoryUsage));

                    if (averageCpuUsage != null && memoryUsage != null)
                        _log.Info(
                            $"High CPU usage ({averageCpuUsage}%) and high memory usage ({memoryUsage}%) detected. " +
                            $"Browser count decreased from {counter.createdHtmlRenderCount + 1} to {counter.createdHtmlRenderCount}."
                        );
                    else if (averageCpuUsage != null)
                        _log.Info(
                            $"High CPU usage ({averageCpuUsage}%) detected. " +
                            $"Browser count decreased from {counter.createdHtmlRenderCount + 1} to {counter.createdHtmlRenderCount}."
                        );
                    else
                        _log.Info(
                            $"High memory usage ({memoryUsage}%) detected. " +
                            $"Browser count decreased from {counter.createdHtmlRenderCount + 1} to {counter.createdHtmlRenderCount}."
                        );
                };
            }

            #endregion Local Functions
        }

        public void Dispose() { _htmlRenderers?.Dispose(); }

        protected override RenderingResult Transform(Resource resource)
        {
            IHtmlRenderer htmlRenderer = null;
            var capturedResources = new List<Resource>();

            try
            {
                if (resource == null)
                    throw new ArgumentNullException(nameof(resource));

                if (!resource.IsExtractedFromHtmlDocument)
                    return ProcessUnsuccessfulRendering(
                        $"{nameof(Resource)} which was not extracted from HTML document was skipped: {resource.ToJson()}",
                        LogLevel.Debug
                    );

                htmlRenderer = _htmlRenderers.Take(_cancellationToken);
                htmlRenderer.OnResourceCaptured += CaptureResource;

                var oldStatusCode = resource.StatusCode;
                var renderingFailed = !htmlRenderer.TryRender(
                    resource,
                    out var htmlText,
                    out var millisecondsPageLoadTime,
                    _cancellationToken
                );

                if (renderingFailed)
                    return ProcessUnsuccessfulRendering(
                        $"Failed to render {nameof(Resource)} was discarded: {resource.ToJson()}",
                        LogLevel.Information
                    );

                UpdateStatusCodeIfChanged();
                if (resource.StatusCode.IsWithinBrokenRange())
                    return ProcessUnsuccessfulRendering(
                        $"Broken {nameof(Resource)} was discarded: {resource.ToJson()}",
                        LogLevel.Information
                    );

                DoStatisticsIfHasPageLoadTime();
                return new RenderingResult
                {
                    RenderedResource = resource,
                    CapturedResources = capturedResources,
                    HtmlDocument = new HtmlDocument { Uri = resource.Uri, HtmlText = htmlText }
                };

                void UpdateStatusCodeIfChanged()
                {
                    var newStatusCode = resource.StatusCode;
                    if (oldStatusCode == newStatusCode) return;
                    if (!VerificationResults.Post(resource.ToVerificationResult()) && !VerificationResults.Completion.IsCompleted)
                        _log.Error($"Failed to post data to buffer block named [{nameof(VerificationResults)}].");
                }
                void DoStatisticsIfHasPageLoadTime()
                {
                    if (!millisecondsPageLoadTime.HasValue) return;
                    _statistics.IncrementSuccessfullyRenderedPageCount();
                    _statistics.IncrementTotalPageLoadTimeBy(millisecondsPageLoadTime.Value);
                }
            }
            catch (Exception exception) when (!exception.IsAcknowledgingOperationCancelledException(_cancellationToken))
            {
                return ProcessUnsuccessfulRendering(
                    $"One or more errors occurred while rendering: {resource.ToJson()}.",
                    LogLevel.Error,
                    exception
                );
            }
            finally
            {
                if (htmlRenderer != null)
                {
                    htmlRenderer.OnResourceCaptured -= CaptureResource;
                    _htmlRenderers.Add(htmlRenderer, CancellationToken.None);
                }
            }

            void CaptureResource(Resource capturedResource) { capturedResources.Add(capturedResource); }
            RenderingResult ProcessUnsuccessfulRendering(string logMessage, LogLevel logLevel, Exception exception = null)
            {
                var failedProcessingResult = new FailedProcessingResult { ProcessedResource = resource };
                if (!FailedProcessingResults.Post(failedProcessingResult) && !FailedProcessingResults.Completion.IsCompleted)
                    _log.Error($"Failed to post data to buffer block named [{nameof(FailedProcessingResults)}].");

                switch (logLevel)
                {
                    case LogLevel.None:
                    case LogLevel.Trace:
                    case LogLevel.Debug:
                        _log.Debug(logMessage, exception);
                        break;
                    case LogLevel.Information:
                        _log.Info(logMessage, exception);
                        break;
                    case LogLevel.Warning:
                        _log.Warn(logMessage, exception);
                        break;
                    case LogLevel.Error:
                        _log.Error(logMessage, exception);
                        break;
                    case LogLevel.Critical:
                        _log.Fatal(logMessage, exception);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
                }

                return null;
            }
        }
    }
}