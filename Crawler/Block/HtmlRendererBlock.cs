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
        readonly CancellationTokenSource _cancellationTokenSource;
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

        public HtmlRendererBlock(IStatistics statistics, ILog log, Configurations configurations, IHardwareMonitor hardwareMonitor,
            Func<IHtmlRenderer> getHtmlRenderer) : base(maxDegreeOfParallelism: configurations.MaxHtmlRendererCount)
        {
            _log = log;
            _statistics = statistics;
            _htmlRenderers = new BlockingCollection<IHtmlRenderer>();
            _cancellationTokenSource = new CancellationTokenSource();
            (int createdHtmlRenderCount, int disposedHtmlRendererCount) counter = (0, 0);

            VerificationResults = new BufferBlock<VerificationResult>();
            FailedProcessingResults = new BufferBlock<FailedProcessingResult>();
            Events = new BufferBlock<Event>(new DataflowBlockOptions { EnsureOrdered = true });

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

        public override void Complete()
        {
            try
            {
                base.Complete();
                _cancellationTokenSource.Cancel();
            }
            catch (Exception exception)
            {
                if (exception.IsAcknowledgingOperationCancelledException(_cancellationTokenSource.Token)) return;
                _log.Error($"One or more errors occurred while completing {nameof(HtmlRendererBlock)}.", exception);
            }
        }

        public void Dispose()
        {
            _htmlRenderers?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        protected override RenderingResult Transform(Resource resource)
        {
            IHtmlRenderer htmlRenderer = null;
            var capturedResources = new List<Resource>();

            try
            {
                if (resource == null)
                    throw new ArgumentNullException(nameof(resource));

                var resourceSizeInMb = resource.Size / 1024f / 1024f;
                if (resourceSizeInMb > 10)
                    return ProcessUnsuccessfulRendering(
                        $"Resource was not queued for rendering because it was too big ({resourceSizeInMb} MB): {resource.ToJson()}",
                        LogLevel.Information
                    );

                if (!resource.IsInternal || resource.ResourceType != ResourceType.Html || !resource.IsExtractedFromHtmlDocument)
                    return ProcessUnsuccessfulRendering(null, LogLevel.None);

                htmlRenderer = _htmlRenderers.Take(_cancellationTokenSource.Token);
                htmlRenderer.OnResourceCaptured += CaptureResource;

                var oldStatusCode = resource.StatusCode;
                var renderingFailed = !htmlRenderer.TryRender(
                    resource,
                    out var htmlText,
                    out var millisecondsPageLoadTime,
                    _cancellationTokenSource.Token
                );

                if (renderingFailed)
                    return ProcessUnsuccessfulRendering(
                        $"Failed to render {nameof(Resource)} was discarded: {resource.ToJson()}",
                        LogLevel.Information
                    );

                UpdateStatusCodeIfChanged();
                if (resource.StatusCode.IsWithinBrokenRange())
                    return ProcessUnsuccessfulRendering(null, LogLevel.None);

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
            catch (Exception exception) when (!exception.IsAcknowledgingOperationCancelledException(_cancellationTokenSource.Token))
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

            #region Local Functions

            void CaptureResource(Resource capturedResource) { capturedResources.Add(capturedResource); }
            RenderingResult ProcessUnsuccessfulRendering(string logMessage, LogLevel logLevel, Exception exception = null)
            {
                var failedProcessingResult = new FailedProcessingResult { ProcessedResource = resource };
                if (!FailedProcessingResults.Post(failedProcessingResult) && !FailedProcessingResults.Completion.IsCompleted)
                    _log.Error($"Failed to post data to buffer block named [{nameof(FailedProcessingResults)}].");

                switch (logLevel)
                {
                    case LogLevel.None:
                        break;
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

            #endregion
        }
    }
}