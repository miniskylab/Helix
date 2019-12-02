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

        public BufferBlock<Resource> BrokenResources { get; }

        public BufferBlock<Event> Events { get; }

        public BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        public BufferBlock<VerificationResult> VerificationResults { get; }

        public override Task Completion => Task.WhenAll(
            base.Completion,
            Events.Completion,
            BrokenResources.Completion,
            VerificationResults.Completion,
            FailedProcessingResults.Completion
        );

        public ResourceVerifierBlock(Configurations configurations, IResourceVerifier resourceVerifier, IStatistics statistics, ILog log,
            IResourceScope resourceScope, IProcessedUrlRegister processedUrlRegister)
            : base(maxDegreeOfParallelism: Configurations.MaxNetworkConnectionCount)
        {
            _log = log;
            _statistics = statistics;
            _resourceScope = resourceScope;
            _configurations = configurations;
            _resourceVerifier = resourceVerifier;
            _processedUrlRegister = processedUrlRegister;
            _cancellationTokenSource = new CancellationTokenSource();

            BrokenResources = new BufferBlock<Resource>();
            VerificationResults = new BufferBlock<VerificationResult>();
            FailedProcessingResults = new BufferBlock<FailedProcessingResult>();
            Events = new BufferBlock<Event>(new DataflowBlockOptions { EnsureOrdered = true });
        }

        public override void Complete()
        {
            try
            {
                base.Complete();
                TryReceiveAll(out _);

                BrokenResources.Complete();
                BrokenResources.TryReceiveAll(out _);

                _cancellationTokenSource.Cancel();
                base.Completion.Wait();

                Events.Complete();
                VerificationResults.Complete();
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
                    return ProcessUnsuccessfulResourceVerification(null, LogLevel.None);

                var verificationResult = resource.IsExtractedFromHtmlDocument
                    ? _resourceVerifier.Verify(resource, _cancellationTokenSource.Token).Result
                    : resource.ToVerificationResult();

                var redirectHappened = !resource.OriginalUri.Equals(resource.Uri);
                if (redirectHappened)
                {
                    if (_resourceScope.IsStartUri(resource.OriginalUri))
                        return ProcessUnsuccessfulResourceVerification(
                            $"Redirect happened. Please provide a start url without any redirect. Or use this url: {resource.Uri.AbsoluteUri}",
                            LogLevel.Error
                        );

                    if (!_processedUrlRegister.TryRegister(resource.Uri.AbsoluteUri))
                        return ProcessUnsuccessfulResourceVerification($"Resource discarded: {resource.ToJson()}", LogLevel.Warning);
                }

                UpdateStatistics();
                SendOutVerificationResult();
                SendOutResourceVerifiedEvent();

                if (!resource.IsInternal) return ProcessUnsuccessfulResourceVerification(null, LogLevel.None);
                return resource.StatusCode.IsWithinBrokenRange() ? BrokenResource() : resource;

                #region Local Functions

                void UpdateStatistics()
                {
                    if (verificationResult.StatusCode.IsWithinBrokenRange()) _statistics.IncrementBrokenUrlCount();
                    else _statistics.IncrementValidUrlCount();
                }
                void SendOutVerificationResult()
                {
                    if (!VerificationResults.Post(verificationResult) && !VerificationResults.Completion.IsCompleted)
                        _log.Error($"Failed to post data to buffer block named [{nameof(VerificationResults)}].");
                }
                void SendOutResourceVerifiedEvent()
                {
                    var statisticsSnapshot = _statistics.TakeSnapshot();
                    var resourceVerifiedEvent = new ResourceVerifiedEvent
                    {
                        ValidUrlCount = statisticsSnapshot.ValidUrlCount,
                        BrokenUrlCount = statisticsSnapshot.BrokenUrlCount,
                        VerifiedUrlCount = statisticsSnapshot.VerifiedUrlCount,
                        Message = $"{verificationResult.StatusCode:D} - {resource.Uri.AbsoluteUri}"
                    };
                    if (!Events.Post(resourceVerifiedEvent) && !Events.Completion.IsCompleted)
                        _log.Error($"Failed to post data to buffer block named [{nameof(Events)}].");
                }
                Resource BrokenResource()
                {
                    if (!BrokenResources.Post(resource) && !BrokenResources.Completion.IsCompleted)
                        _log.Error($"Failed to post data to buffer block named [{nameof(BrokenResources)}].");

                    return null;
                }

                #endregion
            }
            catch (Exception exception)
            {
                return ProcessUnsuccessfulResourceVerification(
                    $"One or more errors occurred while verifying: {resource.ToJson()}.",
                    LogLevel.Error,
                    exception
                );
            }

            #region Local Functions

            Resource ProcessUnsuccessfulResourceVerification(string logMessage, LogLevel logLevel, Exception exception = null)
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
        readonly IStatistics _statistics;
        readonly IResourceScope _resourceScope;
        readonly Configurations _configurations;
        readonly IResourceVerifier _resourceVerifier;
        readonly IProcessedUrlRegister _processedUrlRegister;

        #endregion
    }
}