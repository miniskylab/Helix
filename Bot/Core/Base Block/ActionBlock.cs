using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Helix.Bot
{
    public abstract class ActionBlock<TInput> : ITargetBlock<TInput>
    {
        readonly System.Threading.Tasks.Dataflow.ActionBlock<TInput> _actionBlock;

        public virtual Task Completion => _actionBlock.Completion;

        public int InputCount => _actionBlock.InputCount;

        protected ActionBlock(bool ensureOrdered = false, int maxDegreeOfParallelism = 1)
        {
            _actionBlock = new System.Threading.Tasks.Dataflow.ActionBlock<TInput>(
                Act,
                new ExecutionDataflowBlockOptions
                {
                    EnsureOrdered = ensureOrdered,
                    MaxDegreeOfParallelism = maxDegreeOfParallelism
                }
            );
        }

        public virtual void Complete() { _actionBlock.Complete(); }

        public void Fault(Exception exception) { ((ITargetBlock<TInput>) _actionBlock).Fault(exception); }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, TInput messageValue, ISourceBlock<TInput> source,
            bool consumeToAccept)
        {
            return ((ITargetBlock<TInput>) _actionBlock).OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        protected abstract void Act(TInput input);
    }
}