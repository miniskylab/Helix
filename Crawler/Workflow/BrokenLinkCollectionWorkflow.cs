using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Crawler.Abstractions;
using log4net;

namespace Helix.Crawler
{
    public class BrokenLinkCollectionWorkflow : IBrokenLinkCollectionWorkflow, IDisposable
    {
        readonly ICoordinatorBlock _coordinatorBlock;
        readonly CancellationTokenSource _eventBroadcastCts;
        readonly IEventBroadcasterBlock _eventBroadcasterBlock;
        readonly Task _eventBroadcastTask;
        readonly IHtmlRendererBlock _htmlRendererBlock;
        readonly ILog _log;
        readonly IProcessingResultGeneratorBlock _processingResultGeneratorBlock;
        readonly IReportWriterBlock _reportWriterBlock;
        readonly IResourceVerifierBlock _resourceVerifierBlock;

        public BlockingCollection<Event> Events { get; }

        public int RemainingWorkload => _coordinatorBlock.RemainingWorkload;

        public BrokenLinkCollectionWorkflow(IEventBroadcasterBlock eventBroadcasterBlock, IResourceVerifierBlock resourceVerifierBlock,
            ICoordinatorBlock coordinatorBlock, IReportWriterBlock reportWriterBlock, IHtmlRendererBlock htmlRendererBlock, ILog log,
            IProcessingResultGeneratorBlock processingResultGeneratorBlock)
        {
            _log = log;
            _coordinatorBlock = coordinatorBlock;
            _eventBroadcasterBlock = eventBroadcasterBlock;
            _processingResultGeneratorBlock = processingResultGeneratorBlock;
            _reportWriterBlock = reportWriterBlock;
            _resourceVerifierBlock = resourceVerifierBlock;
            _htmlRendererBlock = htmlRendererBlock;

            Events = new BlockingCollection<Event>();

            _eventBroadcastCts = new CancellationTokenSource();
            _eventBroadcastTask = new Task(() =>
            {
                try
                {
                    while (!_eventBroadcastCts.Token.IsCancellationRequested)
                        Events.Add(_eventBroadcasterBlock.Receive(_eventBroadcastCts.Token));
                }
                catch (Exception exception) when (!exception.IsAcknowledgingOperationCancelledException(_eventBroadcastCts.Token))
                {
                    log.Error("One or more errors occured while broadcast event.", exception);
                }
            }, _eventBroadcastCts.Token);

            WireUpBlocks();

            #region Local Functions

            void WireUpBlocks()
            {
                var generalDataflowLinkOptions = new DataflowLinkOptions { PropagateCompletion = true };

                _coordinatorBlock.LinkTo(NullTarget<Resource>(), PropagateNullObjectsOnly<Resource>());
                _coordinatorBlock.LinkTo(_resourceVerifierBlock, generalDataflowLinkOptions);
                _coordinatorBlock.Events.LinkTo(_eventBroadcasterBlock);

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

            #endregion Local Functions
        }

        public void Dispose()
        {
            _eventBroadcastCts?.Dispose();
            _eventBroadcastTask?.Dispose();
            Events?.Dispose();
        }

        public void SignalShutdown()
        {
            try
            {
                _eventBroadcastCts.Cancel();
                _eventBroadcastTask.Wait();

                _coordinatorBlock.SignalShutdown();
            }
            catch (Exception exception)
            {
                if (exception.IsAcknowledgingOperationCancelledException(_eventBroadcastCts.Token)) return;
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
                    _resourceVerifierBlock.Completion
                ).Wait();
            }
            catch (Exception exception)
            {
                if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                _log.Error("One or more errors occurred while waiting for all blocks to complete.", exception);
            }
        }
    }
}