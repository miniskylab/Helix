using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Bot.Abstractions;
using log4net;

namespace Helix.Bot
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

        public void Dispose() { _cancellationTokenSource?.Dispose(); }

        protected override Resource Transform(Resource resource)
        {
            try
            {
                if (resource == null)
                    throw new ArgumentNullException(nameof(resource));

                var verificationResult = resource.IsExtractedFromHtmlDocument
                    ? _resourceVerifier.Verify(resource, _cancellationTokenSource.Token).Result
                    : resource.ToVerificationResult();

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
                var failedProcessingResult = new FailedProcessingResult { ProcessedResource = resource };
                if (!FailedProcessingResults.Post(failedProcessingResult) && !FailedProcessingResults.Completion.IsCompleted)
                    _log.Error($"Failed to post data to buffer block named [{nameof(FailedProcessingResults)}].");

                _log.Error($"One or more errors occurred while verifying: {resource.ToJson()}.", exception);
                return null;
            }
        }
    }
}