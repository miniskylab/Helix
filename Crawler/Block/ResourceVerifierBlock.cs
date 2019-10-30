using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Crawler.Abstractions;
using log4net;
using Microsoft.Extensions.Logging;

namespace Helix.Crawler
{
    internal class ResourceVerifierBlock : TransformBlock<Resource, Resource>, IResourceVerifierBlock, IDisposable
    {
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly ILog _log;
        readonly IResourceVerifier _resourceVerifier;

        public BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        public BufferBlock<VerificationResult> VerificationResults { get; }

        public override Task Completion => Task.WhenAll(
            base.Completion,
            VerificationResults.Completion,
            FailedProcessingResults.Completion
        );

        public ResourceVerifierBlock(Configurations configurations, IResourceVerifier resourceVerifier, ILog log)
            : base(maxDegreeOfParallelism: configurations.MaxNetworkConnectionCount)
        {
            _log = log;
            _resourceVerifier = resourceVerifier;
            _cancellationTokenSource = new CancellationTokenSource();

            VerificationResults = new BufferBlock<VerificationResult>();
            FailedProcessingResults = new BufferBlock<FailedProcessingResult>();

            base.Completion.ContinueWith(_ =>
            {
                VerificationResults.Complete();
                FailedProcessingResults.Complete();
            });
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
                _log.Error($"One or more errors occurred while completing {nameof(ResourceVerifierBlock)}.", exception);
            }
        }

        public void Dispose()
        {
            _resourceVerifier?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        protected override Resource Transform(Resource resource)
        {
            try
            {
                if (resource == null)
                    throw new ArgumentNullException(nameof(resource));

                VerificationResult verificationResult;
                if (resource.IsExtractedFromHtmlDocument)
                {
                    verificationResult = _resourceVerifier.Verify(resource, _cancellationTokenSource.Token);

                    var isOrphanedUri = verificationResult.StatusCode == StatusCode.OrphanedUri;
                    if (isOrphanedUri)
                        return ProcessUnsuccessfulVerification(
                            $"{nameof(Resource)} with orphaned URL was discarded: {resource.ToJson()}.",
                            LogLevel.Information
                        );

                    var uriSchemeNotSupported = verificationResult.StatusCode == StatusCode.UriSchemeNotSupported;
                    if (uriSchemeNotSupported)
                        return ProcessUnsuccessfulVerification(
                            $"{nameof(Resource)} with unsupported scheme was discarded: {resource.ToJson()}.",
                            LogLevel.Information
                        );
                }
                else verificationResult = resource.ToVerificationResult();

                SendOutVerificationResult();
                return resource;

                #region Local Functions

                void SendOutVerificationResult()
                {
                    if (!VerificationResults.Post(verificationResult) && !VerificationResults.Completion.IsCompleted)
                        _log.Error($"Failed to post data to buffer block named [{nameof(VerificationResults)}].");
                }

                #endregion Local Functions
            }
            catch (Exception exception)
            {
                return ProcessUnsuccessfulVerification(
                    $"One or more errors occurred while verifying: {resource.ToJson()}.",
                    LogLevel.Error,
                    exception
                );
            }

            #region Local Functions

            Resource ProcessUnsuccessfulVerification(string logMessage, LogLevel logLevel, Exception exception = null)
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

            #endregion Local Functions
        }
    }
}