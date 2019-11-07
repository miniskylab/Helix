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

        public ResourceVerifierBlock(Configurations configurations, IResourceVerifier resourceVerifier, IStatistics statistics, ILog log)
            : base(maxDegreeOfParallelism: configurations.MaxNetworkConnectionCount)
        {
            _log = log;
            _statistics = statistics;
            _resourceVerifier = resourceVerifier;
            _cancellationTokenSource = new CancellationTokenSource();

            Events = new BufferBlock<Event>(new DataflowBlockOptions { EnsureOrdered = true });
            VerificationResults = new BufferBlock<VerificationResult>();
            FailedProcessingResults = new BufferBlock<FailedProcessingResult>();

            base.Completion.ContinueWith(_ =>
            {
                Events.Complete();
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

                UpdateStatistics();
                SendOutVerificationResult();
                SendOutResourceVerifiedEvent();
                return resource;

                #region Local Functions

                void SendOutVerificationResult()
                {
                    if (!VerificationResults.Post(verificationResult) && !VerificationResults.Completion.IsCompleted)
                        _log.Error($"Failed to post data to buffer block named [{nameof(VerificationResults)}].");
                }
                void UpdateStatistics()
                {
                    if (verificationResult.StatusCode.IsWithinBrokenRange()) _statistics.IncrementBrokenUrlCount();
                    else _statistics.IncrementValidUrlCount();
                }
                void SendOutResourceVerifiedEvent()
                {
                    var statisticsSnapshot = _statistics.TakeSnapshot();
                    var resourceVerifiedEvent = new ResourceVerifiedEvent
                    {
                        ValidUrlCount = statisticsSnapshot.ValidUrlCount,
                        BrokenUrlCount = statisticsSnapshot.BrokenUrlCount,
                        VerifiedUrlCount = statisticsSnapshot.VerifiedUrlCount,
                        Message = $"{verificationResult.StatusCode:D} - {resource.GetAbsoluteUrl()}"
                    };
                    if (!Events.Post(resourceVerifiedEvent) && !Events.Completion.IsCompleted)
                        _log.Error($"Failed to post data to buffer block named [{nameof(Events)}].");
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