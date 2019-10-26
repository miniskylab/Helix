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
        readonly CancellationToken _cancellationToken;
        readonly ILog _log;
        readonly IResourceVerifier _resourceVerifier;

        public BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        public BufferBlock<VerificationResult> VerificationResults { get; }

        public override Task Completion => Task.WhenAll(
            base.Completion,
            VerificationResults.Completion,
            FailedProcessingResults.Completion
        );

        public ResourceVerifierBlock(CancellationToken cancellationToken, IResourceVerifier resourceVerifier, ILog log)
            : base(cancellationToken, maxDegreeOfParallelism: Environment.ProcessorCount)
        {
            _log = log;
            _resourceVerifier = resourceVerifier;
            _cancellationToken = cancellationToken;

            var dataflowBlockOptions = new DataflowBlockOptions { CancellationToken = cancellationToken };
            VerificationResults = new BufferBlock<VerificationResult>(dataflowBlockOptions);
            FailedProcessingResults = new BufferBlock<FailedProcessingResult>(dataflowBlockOptions);

            base.Completion.ContinueWith(_ =>
            {
                VerificationResults.Complete();
                FailedProcessingResults.Complete();
            });
        }

        public void Dispose() { _resourceVerifier?.Dispose(); }

        protected override Resource Transform(Resource resource)
        {
            try
            {
                if (resource == null)
                    throw new ArgumentNullException(nameof(resource));

                VerificationResult verificationResult = null;
                if (resource.IsExtractedFromHtmlDocument)
                {
                    if (!_resourceVerifier.TryVerify(resource, _cancellationToken, out verificationResult))
                        return ProcessUnsuccessfulVerification(
                            $"Failed to be verified {nameof(Resource)} was discarded: {resource.ToJson()}.",
                            LogLevel.Information
                        );

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

                if (verificationResult == null)
                    verificationResult = resource.ToVerificationResult();

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