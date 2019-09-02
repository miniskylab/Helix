using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Helix.Crawler
{
    public abstract class ActionBlock<TInput> : ITargetBlock<TInput>
    {
        readonly System.Threading.Tasks.Dataflow.ActionBlock<TInput> _actionBlock;

        public Task Completion => _actionBlock.Completion;

        protected ActionBlock(CancellationToken cancellationToken)
        {
            _actionBlock = new System.Threading.Tasks.Dataflow.ActionBlock<TInput>(
                Act,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cancellationToken
                }
            );
        }

        public void Complete() { _actionBlock.Complete(); }

        public void Fault(Exception exception) { ((ITargetBlock<TInput>) _actionBlock).Fault(exception); }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, TInput messageValue, ISourceBlock<TInput> source,
            bool consumeToAccept)
        {
            return ((ITargetBlock<TInput>) _actionBlock).OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        protected abstract void Act(TInput input);
    }
}