using System;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Helix.Bot.Abstractions;
using Helix.Core;
using log4net;

namespace Helix.Bot
{
    public class BrokenLinkCollectionWorkflow : IBrokenLinkCollectionWorkflow, IObserver<Event>
    {
        readonly ICoordinatorBlock _coordinatorBlock;
        readonly IEventBroadcasterBlock _eventBroadcasterBlock;
        readonly JoinBlock<IHtmlRenderer, Resource> _htmlRendererAndBrokenResourceJoinBlock;
        readonly JoinBlock<IHtmlRenderer, Resource> _htmlRendererAndValidResourceJoinBlock;
        readonly IHtmlRendererBlock _htmlRendererBlockForBrokenResources;
        readonly IHtmlRendererBlock _htmlRendererBlockForValidResources;
        readonly ILog _log;
        readonly IProcessingResultGeneratorBlock _processingResultGeneratorBlock;
        readonly IRendererPoolBlock _rendererPoolBlock;
        readonly IReportWriterBlock _reportWriterBlock;
        readonly IResourceVerifierBlock _resourceVerifierBlock;

        public event Action<Event> OnEventBroadcast;

        public BrokenLinkCollectionWorkflow(IEventBroadcasterBlock eventBroadcasterBlock, IResourceVerifierBlock resourceVerifierBlock,
            ICoordinatorBlock coordinatorBlock, IReportWriterBlock reportWriterBlock, IRendererPoolBlock rendererPoolBlock, ILog log,
            IHtmlRendererBlock htmlRendererBlockForBrokenResources, IHtmlRendererBlock htmlRendererBlockForValidResources,
            IProcessingResultGeneratorBlock processingResultGeneratorBlock)
        {
            _log = log;
            _coordinatorBlock = coordinatorBlock;
            _reportWriterBlock = reportWriterBlock;
            _rendererPoolBlock = rendererPoolBlock;
            _eventBroadcasterBlock = eventBroadcasterBlock;
            _resourceVerifierBlock = resourceVerifierBlock;
            _processingResultGeneratorBlock = processingResultGeneratorBlock;
            _htmlRendererBlockForValidResources = htmlRendererBlockForValidResources;
            _htmlRendererBlockForBrokenResources = htmlRendererBlockForBrokenResources;

            var groupingDataflowBlockOptions = new GroupingDataflowBlockOptions { Greedy = false };
            _htmlRendererAndValidResourceJoinBlock = new JoinBlock<IHtmlRenderer, Resource>(groupingDataflowBlockOptions);
            _htmlRendererAndBrokenResourceJoinBlock = new JoinBlock<IHtmlRenderer, Resource>(groupingDataflowBlockOptions);

            _eventBroadcasterBlock.AsObservable().Subscribe(this);

            WireUpBlocks();

            #region Local Functions

            void WireUpBlocks()
            {
                _coordinatorBlock.LinkTo(NullTarget<Resource>(), PropagateNullObjectsOnly<Resource>());
                _coordinatorBlock.LinkTo(_resourceVerifierBlock);
                _coordinatorBlock.Events.LinkTo(_eventBroadcasterBlock);

                _resourceVerifierBlock.LinkTo(NullTarget<Resource>(), PropagateNullObjectsOnly<Resource>());
                _resourceVerifierBlock.LinkTo(_htmlRendererAndValidResourceJoinBlock.Target2);
                _resourceVerifierBlock.BrokenResources.LinkTo(_htmlRendererAndBrokenResourceJoinBlock.Target2);
                _resourceVerifierBlock.Events.LinkTo(_eventBroadcasterBlock);
                _resourceVerifierBlock.ReportWritingMessages.LinkTo(_reportWriterBlock);
                _resourceVerifierBlock.FailedProcessingResults.LinkTo(_coordinatorBlock);

                _rendererPoolBlock.LinkTo(_htmlRendererAndBrokenResourceJoinBlock.Target1);
                _rendererPoolBlock.LinkTo(_htmlRendererAndValidResourceJoinBlock.Target1);
                _rendererPoolBlock.Events.LinkTo(_eventBroadcasterBlock);

                _htmlRendererAndBrokenResourceJoinBlock.LinkTo(_htmlRendererBlockForBrokenResources);
                _htmlRendererAndValidResourceJoinBlock.LinkTo(_htmlRendererBlockForValidResources);

                _htmlRendererBlockForBrokenResources.LinkTo(NullTarget<RenderingResult>(), PropagateNullObjectsOnly<RenderingResult>());
                _htmlRendererBlockForBrokenResources.LinkTo(_processingResultGeneratorBlock);
                _htmlRendererBlockForBrokenResources.ReportWritingMessages.LinkTo(_reportWriterBlock);
                _htmlRendererBlockForBrokenResources.FailedProcessingResults.LinkTo(_coordinatorBlock);
                _htmlRendererBlockForBrokenResources.Events.LinkTo(_eventBroadcasterBlock);
                _htmlRendererBlockForBrokenResources.HtmlRenderers.LinkTo(_rendererPoolBlock);

                _htmlRendererBlockForValidResources.LinkTo(NullTarget<RenderingResult>(), PropagateNullObjectsOnly<RenderingResult>());
                _htmlRendererBlockForValidResources.LinkTo(_processingResultGeneratorBlock);
                _htmlRendererBlockForValidResources.ReportWritingMessages.LinkTo(_reportWriterBlock);
                _htmlRendererBlockForValidResources.FailedProcessingResults.LinkTo(_coordinatorBlock);
                _htmlRendererBlockForValidResources.Events.LinkTo(_eventBroadcasterBlock);
                _htmlRendererBlockForValidResources.HtmlRenderers.LinkTo(_rendererPoolBlock);

                _processingResultGeneratorBlock.LinkTo(NullTarget<ProcessingResult>(), PropagateNullObjectsOnly<ProcessingResult>());
                _processingResultGeneratorBlock.LinkTo(_coordinatorBlock);

                _eventBroadcasterBlock.LinkTo(NullTarget<Event>(), PropagateNullObjectsOnly<Event>());

                #region Local Functions

                Predicate<T> PropagateNullObjectsOnly<T>() { return @object => @object == null; }
                ITargetBlock<T> NullTarget<T>() { return DataflowBlock.NullTarget<T>(); }

                #endregion
            }

            #endregion
        }

        public void OnCompleted()
        {
            /* Do nothing */
        }

        public void OnError(Exception exception)
        {
            _log.Error($"One or more errors occurred while observing data from {nameof(EventBroadcasterBlock)}.", exception);
        }

        public void OnNext(Event @event)
        {
            try { OnEventBroadcast?.Invoke(@event); }
            catch (Exception exception) { _log.Error("One or more errors occurred while broadcast event.", exception); }
        }

        public void Shutdown()
        {
            ShutdownRendererPoolBlockAndResourceVerifierBlock();
            ShutdownJoinBlocks();
            ShutdownHtmlRendererBlocks();
            ShutdownProcessingResultGeneratorBlock();
            ShutdownReportWriterBlock();
            ShutdownCoordinatorBlock();
            ShutdownEventBroadcasterBlock();

            #region Local Functions

            void ShutdownRendererPoolBlockAndResourceVerifierBlock()
            {
                try
                {
                    _rendererPoolBlock.Complete();
                    _resourceVerifierBlock.Complete();

                    _rendererPoolBlock.Completion.Wait();
                    _resourceVerifierBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error(
                        $"One or more errors occurred while shutting down {nameof(RendererPoolBlock)} and {nameof(ResourceVerifierBlock)}.",
                        exception
                    );
                }
            }
            void ShutdownHtmlRendererBlocks()
            {
                try
                {
                    _htmlRendererBlockForBrokenResources.Complete();
                    _htmlRendererBlockForValidResources.Complete();

                    _htmlRendererBlockForBrokenResources.Completion.Wait();
                    _htmlRendererBlockForValidResources.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while shutting down {nameof(HtmlRendererBlock)}.", exception);
                }
            }
            void ShutdownJoinBlocks()
            {
                try
                {
                    _htmlRendererAndBrokenResourceJoinBlock.Complete();
                    _htmlRendererAndValidResourceJoinBlock.Complete();

                    _htmlRendererAndBrokenResourceJoinBlock.Completion.Wait();
                    _htmlRendererAndValidResourceJoinBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while shutting down {nameof(JoinBlock<IHtmlRenderer, Resource>)}.", exception);
                }
            }
            void ShutdownProcessingResultGeneratorBlock()
            {
                try
                {
                    _processingResultGeneratorBlock.Complete();
                    _processingResultGeneratorBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while shutting down {nameof(ProcessingResultGeneratorBlock)}.", exception);
                }
            }
            void ShutdownReportWriterBlock()
            {
                try
                {
                    _reportWriterBlock.Complete();
                    _reportWriterBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while shutting down {nameof(ReportWriterBlock)}.", exception);
                }
            }
            void ShutdownCoordinatorBlock()
            {
                try
                {
                    _coordinatorBlock.Complete();
                    _coordinatorBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while shutting down {nameof(CoordinatorBlock)}.", exception);
                }
            }
            void ShutdownEventBroadcasterBlock()
            {
                try
                {
                    _eventBroadcasterBlock.Complete();
                    _eventBroadcasterBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while shutting down {nameof(EventBroadcasterBlock)}.", exception);
                }
            }

            #endregion
        }

        public bool TryActivate(string startUrl)
        {
            var activationSucceeded = _coordinatorBlock.TryActivateWorkflow(startUrl);
            if (activationSucceeded) _rendererPoolBlock.Activate();

            return activationSucceeded;
        }
    }
}