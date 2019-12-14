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
        readonly JoinBlock<IHtmlRenderer, Resource> _htmlRendererAndResourceJoinBlock;
        readonly IHtmlRendererBlock _htmlRendererBlock;
        readonly ILog _log;
        readonly IPostProcessorBlock _postProcessorBlock;
        readonly IRendererPoolBlock _rendererPoolBlock;
        readonly IReportWriterBlock _reportWriterBlock;
        readonly IResourceVerifierBlock _resourceVerifierBlock;

        public event Action<Event> OnEventBroadcast;

        public BrokenLinkCollectionWorkflow(IEventBroadcasterBlock eventBroadcasterBlock, IResourceVerifierBlock resourceVerifierBlock,
            ICoordinatorBlock coordinatorBlock, IReportWriterBlock reportWriterBlock, IRendererPoolBlock rendererPoolBlock, ILog log,
            IHtmlRendererBlock htmlRendererBlock, IPostProcessorBlock postProcessorBlock)
        {
            _log = log;
            _coordinatorBlock = coordinatorBlock;
            _reportWriterBlock = reportWriterBlock;
            _rendererPoolBlock = rendererPoolBlock;
            _eventBroadcasterBlock = eventBroadcasterBlock;
            _resourceVerifierBlock = resourceVerifierBlock;
            _postProcessorBlock = postProcessorBlock;
            _htmlRendererBlock = htmlRendererBlock;

            _htmlRendererAndResourceJoinBlock = new JoinBlock<IHtmlRenderer, Resource>(new GroupingDataflowBlockOptions { Greedy = false });
            _eventBroadcasterBlock.AsObservable().Subscribe(this);

            WireUpBlocks();

            #region Local Functions

            void WireUpBlocks()
            {
                _coordinatorBlock.LinkTo(NullTarget<Resource>(), PropagateNullObjectsOnly<Resource>());
                _coordinatorBlock.LinkTo(_resourceVerifierBlock);
                _coordinatorBlock.Events.LinkTo(_eventBroadcasterBlock);
                _coordinatorBlock.VerificationResults.LinkTo(_reportWriterBlock);

                _resourceVerifierBlock.LinkTo(NullTarget<Resource>(), PropagateNullObjectsOnly<Resource>());
                _resourceVerifierBlock.LinkTo(_htmlRendererAndResourceJoinBlock.Target2);
                _resourceVerifierBlock.FailedProcessingResults.LinkTo(_coordinatorBlock);

                _rendererPoolBlock.LinkTo(_htmlRendererAndResourceJoinBlock.Target1);
                _rendererPoolBlock.Events.LinkTo(_eventBroadcasterBlock);

                _htmlRendererAndResourceJoinBlock.LinkTo(_htmlRendererBlock);

                _htmlRendererBlock.LinkTo(NullTarget<RenderingResult>(), PropagateNullObjectsOnly<RenderingResult>());
                _htmlRendererBlock.LinkTo(_postProcessorBlock);
                _htmlRendererBlock.FailedProcessingResults.LinkTo(_coordinatorBlock);
                _htmlRendererBlock.HtmlRenderers.LinkTo(_rendererPoolBlock);

                _postProcessorBlock.LinkTo(NullTarget<ProcessingResult>(), PropagateNullObjectsOnly<ProcessingResult>());
                _postProcessorBlock.LinkTo(_coordinatorBlock);

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
            ShutdownResourceVerifierBlock();
            ShutdownJoinBlock();
            ShutdownHtmlRendererBlock();
            ShutdownRendererPoolBlock();
            ShutdownPostProcessorBlock();
            ShutdownReportWriterBlock();
            ShutdownCoordinatorBlock();
            ShutdownEventBroadcasterBlock();

            #region Local Functions

            void ShutdownResourceVerifierBlock()
            {
                try
                {
                    _resourceVerifierBlock.Complete();
                    _resourceVerifierBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while shutting down {nameof(ResourceVerifierBlock)}.", exception);
                }
            }
            void ShutdownJoinBlock()
            {
                try
                {
                    _htmlRendererAndResourceJoinBlock.Complete();
                    _htmlRendererAndResourceJoinBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while shutting down {nameof(JoinBlock<IHtmlRenderer, Resource>)}.", exception);
                }
            }
            void ShutdownHtmlRendererBlock()
            {
                try
                {
                    _htmlRendererBlock.Complete();
                    _htmlRendererBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while shutting down {nameof(HtmlRendererBlock)}.", exception);
                }
            }
            void ShutdownRendererPoolBlock()
            {
                try
                {
                    _rendererPoolBlock.Complete();
                    _rendererPoolBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while shutting down {nameof(RendererPoolBlock)}.", exception);
                }
            }
            void ShutdownPostProcessorBlock()
            {
                try
                {
                    _postProcessorBlock.Complete();
                    _postProcessorBlock.Completion.Wait();
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(CancellationToken.None)) return;
                    _log.Error($"One or more errors occurred while shutting down {nameof(PostProcessorBlock)}.", exception);
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