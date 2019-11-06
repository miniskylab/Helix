using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Bot.Abstractions;
using log4net;

namespace Helix.Bot
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

        public BrokenLinkCollectionWorkflow(IEventBroadcasterBlock eventBroadcasterBlock, IResourceVerifierBlock resourceVerifierBlock,
            ICoordinatorBlock coordinatorBlock, IReportWriterBlock reportWriterBlock, IHtmlRendererBlock htmlRendererBlock, ILog log,
            IProcessingResultGeneratorBlock processingResultGeneratorBlock)
        {
            _log = log;
            _coordinatorBlock = coordinatorBlock;
            _htmlRendererBlock = htmlRendererBlock;
            _reportWriterBlock = reportWriterBlock;
            _eventBroadcasterBlock = eventBroadcasterBlock;
            _resourceVerifierBlock = resourceVerifierBlock;
            _processingResultGeneratorBlock = processingResultGeneratorBlock;

            Events = new BlockingCollection<Event>();

            _eventBroadcastCts = new CancellationTokenSource();
            _eventBroadcastTask = new Task(BroadcastEvents, _eventBroadcastCts.Token);

            WireUpBlocks();

            #region Local Functions

            void WireUpBlocks()
            {
                _coordinatorBlock.LinkTo(NullTarget<Resource>(), PropagateNullObjectsOnly<Resource>());
                _coordinatorBlock.LinkTo(_resourceVerifierBlock);
                _coordinatorBlock.Events.LinkTo(_eventBroadcasterBlock);

                _resourceVerifierBlock.LinkTo(NullTarget<Resource>(), PropagateNullObjectsOnly<Resource>());
                _resourceVerifierBlock.LinkTo(_htmlRendererBlock);
                _resourceVerifierBlock.Events.LinkTo(_eventBroadcasterBlock);
                _resourceVerifierBlock.VerificationResults.LinkTo(_reportWriterBlock);
                _resourceVerifierBlock.FailedProcessingResults.LinkTo(_coordinatorBlock);

                _htmlRendererBlock.LinkTo(NullTarget<RenderingResult>(), PropagateNullObjectsOnly<RenderingResult>());
                _htmlRendererBlock.LinkTo(_processingResultGeneratorBlock);
                _htmlRendererBlock.VerificationResults.LinkTo(_reportWriterBlock);
                _htmlRendererBlock.FailedProcessingResults.LinkTo(_coordinatorBlock);
                _htmlRendererBlock.Events.LinkTo(_eventBroadcasterBlock);

                _processingResultGeneratorBlock.LinkTo(NullTarget<ProcessingResult>(), PropagateNullObjectsOnly<ProcessingResult>());
                _processingResultGeneratorBlock.LinkTo(_coordinatorBlock);

                _eventBroadcasterBlock.LinkTo(NullTarget<Event>(), PropagateNullObjectsOnly<Event>());

                #region Local Functions

                Predicate<T> PropagateNullObjectsOnly<T>() { return @object => @object == null; }
                ITargetBlock<T> NullTarget<T>() { return DataflowBlock.NullTarget<T>(); }

                #endregion Local Functions
            }
            void BroadcastEvents()
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
            }

            #endregion Local Functions
        }

        public void Dispose()
        {
            _eventBroadcastCts?.Dispose();
            _eventBroadcastTask?.Dispose();
            Events?.Dispose();
        }

        public void Shutdown()
        {
            CompleteResourceVerifierBlock();
            CompleteHtmlRendererBlock();
            CompleteProcessingResultGeneratorBlock();
            CompleteReportWriterBlock();
            CompleteCoordinatorBlock();
            CancelEventBroadcastTask();
            CompleteEventBroadcasterBlock();

            #region Local Functions

            void CompleteResourceVerifierBlock()
            {
                try
                {
                    _resourceVerifierBlock.Complete();
                    _resourceVerifierBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while completing {nameof(ResourceVerifierBlock)}.", exception);
                }
            }
            void CompleteHtmlRendererBlock()
            {
                try
                {
                    _htmlRendererBlock.Complete();
                    _htmlRendererBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while completing {nameof(HtmlRendererBlock)}.", exception);
                }
            }
            void CompleteProcessingResultGeneratorBlock()
            {
                try
                {
                    _processingResultGeneratorBlock.Complete();
                    _processingResultGeneratorBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while completing {nameof(ProcessingResultGeneratorBlock)}.", exception);
                }
            }
            void CompleteReportWriterBlock()
            {
                try
                {
                    _reportWriterBlock.Complete();
                    _reportWriterBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while completing {nameof(ReportWriterBlock)}.", exception);
                }
            }
            void CompleteCoordinatorBlock()
            {
                try
                {
                    _coordinatorBlock.Complete();
                    _coordinatorBlock.TryReceiveAll(out _);
                    _coordinatorBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while completing {nameof(CoordinatorBlock)}.", exception);
                }
            }
            void CancelEventBroadcastTask()
            {
                try
                {
                    _eventBroadcastCts.Cancel();
                    _eventBroadcastTask.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(_eventBroadcastCts.Token)) return;
                    _log.Error("One or more errors occurred while cancelling event broadcast task.", exception);
                }
            }
            void CompleteEventBroadcasterBlock()
            {
                try
                {
                    _eventBroadcasterBlock.Complete();
                    _eventBroadcasterBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while completing {nameof(EventBroadcasterBlock)}.", exception);
                }
            }

            #endregion
        }

        public bool TryActivate(string startUrl)
        {
            var activationSucceeded = _coordinatorBlock.TryActivateWorkflow(startUrl);
            if (activationSucceeded) _eventBroadcastTask.Start();

            return activationSucceeded;
        }
    }
}