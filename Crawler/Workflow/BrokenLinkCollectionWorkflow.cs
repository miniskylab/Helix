using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Crawler.Abstractions;
using log4net;

namespace Helix.Crawler
{
    internal class BrokenLinkCollectionWorkflow : IBrokenLinkCollectionWorkflow
    {
        readonly ICoordinatorBlock _coordinatorBlock;
        readonly CancellationTokenSource _eventBroadcastCancellationTokenSource;
        readonly IEventBroadcasterBlock _eventBroadcasterBlock;
        readonly Task _eventBroadcastTask;
        readonly IHtmlRendererBlock _htmlRendererBlock;
        readonly ILog _log;
        readonly IProcessingResultGeneratorBlock _processingResultGeneratorBlock;
        readonly IReportWriterBlock _reportWriterBlock;
        readonly IResourceEnricherBlock _resourceEnricherBlock;
        readonly IResourceVerifierBlock _resourceVerifierBlock;

        public event Action<Event> OnEventBroadcast;

        public BrokenLinkCollectionWorkflow(IResourceVerifierBlock resourceVerifierBlock, IEventBroadcasterBlock eventBroadcasterBlock,
            ICoordinatorBlock coordinatorBlock, IReportWriterBlock reportWriterBlock, IResourceEnricherBlock resourceEnricherBlock,
            IProcessingResultGeneratorBlock processingResultGeneratorBlock, IHtmlRendererBlock htmlRendererBlock, ILog log)
        {
            _log = log;
            _coordinatorBlock = coordinatorBlock;
            _eventBroadcasterBlock = eventBroadcasterBlock;
            _processingResultGeneratorBlock = processingResultGeneratorBlock;
            _reportWriterBlock = reportWriterBlock;
            _resourceEnricherBlock = resourceEnricherBlock;
            _resourceVerifierBlock = resourceVerifierBlock;
            _htmlRendererBlock = htmlRendererBlock;

            _eventBroadcastCancellationTokenSource = new CancellationTokenSource();
            _eventBroadcastTask = new Task(
                () =>
                {
                    var cancellationToken = _eventBroadcastCancellationTokenSource.Token;
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                            OnEventBroadcast?.Invoke(_eventBroadcasterBlock.Receive(cancellationToken));
                    }
                    catch (Exception exception) when (!exception.IsAcknowledgingOperationCancelledException(cancellationToken))
                    {
                        log.Error("One or more errors occured while broadcast event.", exception);
                    }
                },
                _eventBroadcastCancellationTokenSource.Token
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
            try { _coordinatorBlock.SignalShutdown(); }
            catch (Exception exception)
            {
                _log.Error($"One or more errors occurred while signaling shutdown for {nameof(BrokenLinkCollectionWorkflow)}.", exception);
            }
        }

        public bool TryActivate(string startUrl)
        {
            var activationSucceeded = _coordinatorBlock.TryActivateWorkflow(startUrl);
            if (activationSucceeded) _eventBroadcastTask.Start();

            return activationSucceeded;
        }

        public void WaitForCompletion()
        {
            try
            {
                Task.WhenAll(
                    _coordinatorBlock.Completion,
                    _eventBroadcasterBlock.Completion,
                    _htmlRendererBlock.Completion,
                    _processingResultGeneratorBlock.Completion,
                    _reportWriterBlock.Completion,
                    _resourceEnricherBlock.Completion,
                    _resourceVerifierBlock.Completion
                ).Wait();
            }
            catch (Exception exception)
            {
                if (exception.IsAcknowledgingOperationCancelledException(_eventBroadcastCancellationTokenSource.Token)) return;
                _log.Error("One or more errors occurred while waiting for all blocks to complete.", exception);
            }

            try
            {
                _eventBroadcastCancellationTokenSource.Cancel();
                _eventBroadcastTask.Wait();

                _eventBroadcastCancellationTokenSource.Dispose();
                _eventBroadcastTask.Dispose();
            }
            catch (Exception exception)
            {
                if (exception.IsAcknowledgingOperationCancelledException(_eventBroadcastCancellationTokenSource.Token)) return;
                _log.Error("One or more errors occurred while stopping event broadcast task.", exception);
            }
        }
    }
}