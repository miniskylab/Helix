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
    internal class ResourceVerifierBlock : TransformBlock<Resource, Resource>, IResourceVerifierBlock, IDisposable
    {
        readonly CancellationTokenSource _cancellationTokenSource;

        public BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        public override Task Completion => Task.WhenAll(
            base.Completion,
            FailedProcessingResults.Completion
        );

        public ResourceVerifierBlock(Configurations configurations, IResourceVerifier resourceVerifier, ILog log)
            : base(false, Configurations.MaxNetworkConnectionCount)
        {
            _log = log;
            _configurations = configurations;
            _resourceVerifier = resourceVerifier;
            _cancellationTokenSource = new CancellationTokenSource();

            FailedProcessingResults = new BufferBlock<FailedProcessingResult>();
        }

        public override void Complete()
        {
            try
            {
                base.Complete();
                TryReceiveAll(out _);

                _cancellationTokenSource.Cancel();
                base.Completion.Wait();

                FailedProcessingResults.Complete();
            }
            catch (Exception exception)
            {
                if (exception.IsAcknowledgingOperationCancelledException(_cancellationTokenSource.Token)) return;
                _log.Error($"One or more errors occurred while completing {nameof(ResourceVerifierBlock)}.", exception);
            }
        }

        public void Dispose() { _cancellationTokenSource?.Dispose(); }

        protected override Resource Transform(Resource resource)
        {
            try
            {
                if (resource == null)
                    throw new ArgumentNullException(nameof(resource));

                if (resource.StatusCode == StatusCode.UriSchemeNotSupported && !_configurations.IncludeNonHttpUrlsInReport)
                    return FailedProcessingResult(null, LogLevel.None);

                var oldUri = resource.Uri;
                if (resource.IsExtractedFromHtmlDocument)
                    _resourceVerifier.Verify(resource, _cancellationTokenSource.Token).Wait();

                var redirectHappened = resource.Uri != oldUri;
                if (redirectHappened) return FailedProcessingResult(null, LogLevel.None);

                return resource.IsInternal ? resource : FailedProcessingResult(null, LogLevel.None);
            }
            catch (Exception exception)
            {
                return FailedProcessingResult(
                    $"One or more errors occurred while verifying: {resource.ToJson()}.",
                    LogLevel.Error,
                    exception
                );
            }

            #region Local Functions

            Resource FailedProcessingResult(string logMessage, LogLevel logLevel, Exception exception = null)
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

        #region Injected Services

        readonly ILog _log;
        readonly Configurations _configurations;
        readonly IResourceVerifier _resourceVerifier;

        #endregion
    }
}