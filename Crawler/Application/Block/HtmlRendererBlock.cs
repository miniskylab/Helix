using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Crawler.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Crawler
{
    internal class HtmlRendererBlock : TransformBlock<Resource, HtmlDocument>
    {
        readonly CancellationToken _cancellationToken;
        readonly BlockingCollection<IHtmlRenderer> _htmlRenderers;
        readonly ILog _log;
        readonly IStatistics _statistics;

        public BufferBlock<Resource> CapturedResources { get; }

        public BufferBlock<Event> Events { get; }

        public BufferBlock<VerificationResult> VerificationResults { get; }

        public override Task Completion => Task.WhenAll(
            base.Completion,
            Events.Completion,
            CapturedResources.Completion,
            VerificationResults.Completion
        );

        public HtmlRendererBlock(CancellationToken cancellationToken, IStatistics statistics, ILog log, Configurations configurations,
            IHardwareMonitor hardwareMonitor, Func<IHtmlRenderer> getHtmlRenderer) : base(cancellationToken)
        {
            _log = log;
            _statistics = statistics;
            _cancellationToken = cancellationToken;
            _htmlRenderers = new BlockingCollection<IHtmlRenderer>();
            (int createdHtmlRenderCount, int disposedHtmlRendererCount) counter = (0, 0);

            Events = new BufferBlock<Event>(new DataflowBlockOptions { EnsureOrdered = true, CancellationToken = cancellationToken });
            CapturedResources = new BufferBlock<Resource>(new DataflowBlockOptions { CancellationToken = cancellationToken });
            VerificationResults = new BufferBlock<VerificationResult>(new DataflowBlockOptions { CancellationToken = cancellationToken });

            base.Completion.ContinueWith(_ =>
            {
                CapturedResources.Complete();
                VerificationResults.Complete();
                DisposeHtmlRenderers();
                Events.Complete();
                CheckMemoryLeak();

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
                        if (!Events.Post(webBrowserClosedEvent))
                            _log.Error($"Failed to post data to buffer block named [{nameof(Events)}].");
                    }
                    _htmlRenderers?.Dispose();
                }
            });

            CreateHtmlRenderer();
            CreateAndDestroyHtmlRenderersAdaptively();

            void CreateHtmlRenderer()
            {
                var htmlRenderer = getHtmlRenderer();
                htmlRenderer.OnResourceCaptured += resource =>
                {
                    if (!CapturedResources.Post(resource))
                        _log.Error($"Failed to post data to buffer block named [{nameof(CapturedResources)}].");
                };

                _htmlRenderers.Add(htmlRenderer, CancellationToken.None);
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
        }

        protected override HtmlDocument Transform(Resource resource)
        {
            IHtmlRenderer htmlRenderer = null;
            try
            {
                htmlRenderer = _htmlRenderers.Take(_cancellationToken);
                var oldStatusCode = resource.StatusCode;
                var renderingFailed = !htmlRenderer.TryRender(
                    resource,
                    out var htmlText,
                    out var millisecondsPageLoadTime,
                    _cancellationToken
                );

                if (renderingFailed)
                {
                    _log.Info($"Failed to render {nameof(Resource)} was discarded: {JsonConvert.SerializeObject(resource)}");
                    return null;
                }

                UpdateStatusCodeIfChanged();
                DoStatisticsIfHasPageLoadTime();

                if (!resource.StatusCode.IsWithinBrokenRange())
                    return new HtmlDocument { Uri = resource.Uri, Text = htmlText };

                _log.Info($"Broken {nameof(Resource)} was discarded: {JsonConvert.SerializeObject(resource)}");
                return null;

                void UpdateStatusCodeIfChanged()
                {
                    var newStatusCode = resource.StatusCode;
                    if (oldStatusCode == newStatusCode) return;
                    if (!VerificationResults.Post(resource.ToVerificationResult()))
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
                _log.Error($"One or more errors occurred while rendering: {JsonConvert.SerializeObject(resource)}.", exception);
                return null;
            }
            finally
            {
                if (htmlRenderer != null) _htmlRenderers.Add(htmlRenderer, CancellationToken.None);
            }
        }
    }
}