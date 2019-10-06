using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Crawler.Abstractions;
using log4net;

namespace Helix.Crawler
{
    internal class BrokenLinkCollectionWorkflow
    {
        readonly CoordinatorBlock _coordinatorBlock;
        readonly EventBroadcasterBlock _eventBroadcasterBlock;
        readonly HtmlRendererBlock _htmlRendererBlock;
        readonly ILog _log;
        readonly ProcessingResultGeneratorBlock _processingResultGeneratorBlock;
        readonly ReportWriterBlock _reportWriterBlock;
        readonly ResourceEnricherBlock _resourceEnricherBlock;
        readonly ResourceVerifierBlock _resourceVerifierBlock;

        public BrokenLinkCollectionWorkflow(CancellationToken cancellationToken, Configurations configurations, IStatistics statistics,
            IHardwareMonitor hardwareMonitor, IResourceExtractor resourceExtractor, IReportWriter reportWriter, ILog log,
            IResourceEnricher resourceEnricher, IResourceVerifier resourceVerifier, Func<IHtmlRenderer> getHtmlRenderer)
        {
            _log = log;
            _coordinatorBlock = new CoordinatorBlock(cancellationToken, log);
            _eventBroadcasterBlock = new EventBroadcasterBlock(cancellationToken);
            _processingResultGeneratorBlock = new ProcessingResultGeneratorBlock(cancellationToken, resourceExtractor, log);
            _reportWriterBlock = new ReportWriterBlock(cancellationToken, reportWriter, log);
            _resourceEnricherBlock = new ResourceEnricherBlock(cancellationToken, resourceEnricher, log);
            _resourceVerifierBlock = new ResourceVerifierBlock(cancellationToken, statistics, resourceVerifier, log);
            _htmlRendererBlock = new HtmlRendererBlock(
                cancellationToken,
                statistics,
                log,
                configurations,
                hardwareMonitor,
                getHtmlRenderer
            );

            WireUpBlocks();

            void WireUpBlocks()
            {
                var generalDataflowLinkOptions = new DataflowLinkOptions { PropagateCompletion = true };

                _coordinatorBlock.LinkTo(NullTarget<Resource>(), PropagateNullObjectsOnly<Resource>());
                _coordinatorBlock.LinkTo(_resourceEnricherBlock, generalDataflowLinkOptions);

                _resourceEnricherBlock.LinkTo(NullTarget<Resource>(), PropagateNullObjectsOnly<Resource>());
                _resourceEnricherBlock.LinkTo(_resourceVerifierBlock, generalDataflowLinkOptions);
                _resourceEnricherBlock.FailedProcessingResults.LinkTo(_coordinatorBlock);

                _resourceVerifierBlock.LinkTo(NullTarget<Resource>(), PropagateNullObjectsOnly<Resource>());
                _resourceVerifierBlock.LinkTo(_htmlRendererBlock, generalDataflowLinkOptions);
                _resourceVerifierBlock.FailedProcessingResults.LinkTo(_coordinatorBlock);
                _resourceVerifierBlock.VerificationResults.LinkTo(_reportWriterBlock);
                _resourceVerifierBlock.Events.LinkTo(_eventBroadcasterBlock);

                _htmlRendererBlock.LinkTo(NullTarget<RenderingResult>(), PropagateNullObjectsOnly<RenderingResult>());
                _htmlRendererBlock.LinkTo(_processingResultGeneratorBlock, generalDataflowLinkOptions);
                _htmlRendererBlock.VerificationResults.LinkTo(_reportWriterBlock, generalDataflowLinkOptions);
                _htmlRendererBlock.FailedProcessingResults.LinkTo(_coordinatorBlock);
                _htmlRendererBlock.Events.LinkTo(_eventBroadcasterBlock, generalDataflowLinkOptions);

                _processingResultGeneratorBlock.LinkTo(NullTarget<ProcessingResult>(), PropagateNullObjectsOnly<ProcessingResult>());
                _processingResultGeneratorBlock.LinkTo(_coordinatorBlock);

                _eventBroadcasterBlock.LinkTo(NullTarget<Event>(), PropagateNullObjectsOnly<Event>());

                Predicate<T> PropagateNullObjectsOnly<T>() { return @object => @object == null; }
                ITargetBlock<T> NullTarget<T>() { return DataflowBlock.NullTarget<T>(); }
            }
        }

        public void SignalShutdown()
        {
            try
            {
                _coordinatorBlock.SignalShutdown();
                Task.WhenAll(
                    _coordinatorBlock.Completion,
                    _eventBroadcasterBlock.Completion,
                    _htmlRendererBlock.Completion,
                    _processingResultGeneratorBlock.Completion,
                    _reportWriterBlock.Completion,
                    _resourceEnricherBlock.Completion,
                    _resourceVerifierBlock.Completion
                );
            }
            catch (Exception exception)
            {
                _log.Error($"One or more errors occurred while signaling shutdown for {nameof(BrokenLinkCollectionWorkflow)}.", exception);
            }
        }

        public bool TryActivate(string startUrl) { return _coordinatorBlock.TryActivateWorkflow(startUrl); }
    }
}