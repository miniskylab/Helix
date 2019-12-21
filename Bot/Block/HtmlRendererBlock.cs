using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Bot.Abstractions;
using Helix.Core;
using log4net;
using Microsoft.Extensions.Logging;

namespace Helix.Bot
{
    public class HtmlRendererBlock : TransformBlock<Tuple<IHtmlRenderer, Resource>, RenderingResult>,
                                     IHtmlRendererBlock, IDisposable
    {
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly ILog _log;

        public BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        public BufferBlock<IHtmlRenderer> HtmlRenderers { get; }

        public override Task Completion => Task.WhenAll(
            base.Completion,
            HtmlRenderers.Completion,
            FailedProcessingResults.Completion
        );

        public HtmlRendererBlock(ILog log) : base(maxDegreeOfParallelism: Configurations.MaxHtmlRendererCount)
        {
            _log = log;
            _cancellationTokenSource = new CancellationTokenSource();

            HtmlRenderers = new BufferBlock<IHtmlRenderer>();
            FailedProcessingResults = new BufferBlock<FailedProcessingResult>();
        }

        public override void Complete()
        {
            try
            {
                base.Complete();
                _cancellationTokenSource.Cancel();

                base.Completion.Wait();
                HtmlRenderers.Complete();
                FailedProcessingResults.Complete();
            }
            catch (Exception exception)
            {
                if (exception.IsAcknowledgingOperationCancelledException(_cancellationTokenSource.Token)) return;
                _log.Error($"One or more errors occurred while completing {nameof(HtmlRendererBlock)}.", exception);
            }
        }

        public void Dispose() { _cancellationTokenSource?.Dispose(); }

        protected override RenderingResult Transform(Tuple<IHtmlRenderer, Resource> htmlRendererAndResource)
        {
            var (htmlRenderer, resource) = htmlRendererAndResource ?? throw new ArgumentNullException(nameof(htmlRendererAndResource));
            try
            {
                if (!resource.StatusCode.IsWithinBrokenRange())
                {
                    var resourceSizeInMb = resource.Size / 1024f / 1024f;
                    if (resourceSizeInMb > Configurations.RenderableResourceSizeInMb)
                        return FailedProcessingResult(
                            $"Resource was not queued for rendering because it was too big ({resourceSizeInMb} MB): {resource.ToJson()}",
                            LogLevel.Debug
                        );

                    var resourceTypeIsNotRenderable = !(ResourceType.Html | ResourceType.Unknown).HasFlag(resource.ResourceType);
                    if (!resource.IsInternal || !resource.IsExtractedFromHtmlDocument || resourceTypeIsNotRenderable)
                        return FailedProcessingResult(null, LogLevel.None);
                }

                var renderingFailed = !htmlRenderer.TryRender(
                    resource,
                    out var htmlText,
                    out var millisecondsPageLoadTime,
                    out var capturedResources,
                    _cancellationTokenSource.Token
                );

                if (renderingFailed)
                    return FailedProcessingResult(
                        $"Failed to render {nameof(Resource)}: {resource.ToJson()}",
                        LogLevel.Information
                    );

                if (resource.StatusCode.IsWithinBrokenRange())
                    return FailedProcessingResult(null, LogLevel.None);

                return new RenderingResult
                {
                    RenderedResource = resource,
                    CapturedResources = capturedResources,
                    MillisecondsPageLoadTime = millisecondsPageLoadTime,
                    HtmlDocument = new HtmlDocument { Uri = resource.Uri, HtmlText = htmlText }
                };
            }
            catch (Exception exception) when (!exception.IsAcknowledgingOperationCancelledException(_cancellationTokenSource.Token))
            {
                return FailedProcessingResult(
                    $"One or more errors occurred while rendering: {resource.ToJson()}.",
                    LogLevel.Error,
                    exception
                );
            }
            finally
            {
                if (!HtmlRenderers.Post(htmlRenderer) && !HtmlRenderers.Completion.IsCompleted)
                    _log.Error($"Failed to post data to buffer block named [{nameof(HtmlRenderers)}].");
            }

            #region Local Functions

            RenderingResult FailedProcessingResult(string logMessage, LogLevel logLevel, Exception exception = null)
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