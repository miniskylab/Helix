using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Crawler.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Crawler
{
    internal class ResourceVerifierBlock : TransformBlock<Resource, Resource>
    {
        readonly CancellationToken _cancellationToken;
        readonly ILog _log;
        readonly IResourceVerifier _resourceVerifier;
        readonly IStatistics _statistics;

        public BufferBlock<Event> Events { get; }

        public BufferBlock<VerificationResult> VerificationResults { get; }

        public override Task Completion => Task.WhenAll(base.Completion, VerificationResults.Completion, Events.Completion);

        public ResourceVerifierBlock(CancellationToken cancellationToken, IStatistics statistics, IResourceVerifier resourceVerifier,
            ILog log) : base(cancellationToken)
        {
            _log = log;
            _statistics = statistics;
            _resourceVerifier = resourceVerifier;
            _cancellationToken = cancellationToken;

            Events = new BufferBlock<Event>(new DataflowBlockOptions { EnsureOrdered = true, CancellationToken = cancellationToken });
            VerificationResults = new BufferBlock<VerificationResult>(new DataflowBlockOptions { CancellationToken = cancellationToken });

            base.Completion.ContinueWith(_ =>
            {
                Events.Complete();
                VerificationResults.Complete();
            });
        }

        protected override Resource Transform(Resource resource)
        {
            try
            {
                if (!_resourceVerifier.TryVerify(resource, _cancellationToken, out var verificationResult))
                {
                    _log.Info($"Failed to be verified {nameof(Resource)} was discarded: {JsonConvert.SerializeObject(resource)}.");
                    return null;
                }

                var isOrphanedUri = verificationResult.StatusCode == StatusCode.OrphanedUri;
                if (isOrphanedUri)
                {
                    _log.Info($"{nameof(Resource)} with orphaned URL was discarded: {JsonConvert.SerializeObject(resource)}.");
                    return null;
                }

                var uriSchemeNotSupported = verificationResult.StatusCode == StatusCode.UriSchemeNotSupported;
                if (uriSchemeNotSupported)
                {
                    _log.Info($"{nameof(Resource)} with unsupported scheme was discarded: {JsonConvert.SerializeObject(resource)}.");
                    return null;
                }

                DoStatistics();
                SendOutVerificationResult();
                SendOutResourceVerifiedEvent();

                return resource;

                void DoStatistics()
                {
                    if (resource.StatusCode.IsWithinBrokenRange()) _statistics.IncrementBrokenUrlCount();
                    else _statistics.IncrementValidUrlCount();
                }
                void SendOutVerificationResult()
                {
                    if (!VerificationResults.Post(verificationResult))
                        _log.Error($"Failed to post data to buffer block named [{nameof(VerificationResults)}].");
                }
                void SendOutResourceVerifiedEvent()
                {
                    var resourceVerifiedEvent = new Event
                    {
                        EventType = EventType.ResourceVerified,
                        Message = $"{verificationResult.StatusCode:D} - {verificationResult.VerifiedUrl}"
                    };
                    if (!Events.Post(resourceVerifiedEvent))
                        _log.Error($"Failed to post data to buffer block named [{nameof(Events)}].");
                }
            }
            catch (Exception exception)
            {
                _log.Error($"One or more errors occurred while verifying: {JsonConvert.SerializeObject(resource)}.", exception);
                return null;
            }
        }
    }
}