using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Helix.Bot
{
    public abstract class TransformManyBlock<TInput, TOutput> : IPropagatorBlock<TInput, TOutput>, IReceivableSourceBlock<TOutput>
    {
        readonly System.Threading.Tasks.Dataflow.TransformManyBlock<TInput, TOutput> _transformManyBlock;

        public virtual Task Completion => _transformManyBlock.Completion;

        public int InputCount => _transformManyBlock.InputCount;

        public int OutputCount => _transformManyBlock.OutputCount;

        protected TransformManyBlock(bool ensureOrdered = false, int maxDegreeOfParallelism = 1)
        {
            _transformManyBlock = new System.Threading.Tasks.Dataflow.TransformManyBlock<TInput, TOutput>(
                input => Transform(input),
                new ExecutionDataflowBlockOptions
                {
                    EnsureOrdered = ensureOrdered,
                    MaxDegreeOfParallelism = maxDegreeOfParallelism
                }
            );
        }

        public virtual void Complete() { _transformManyBlock.Complete(); }

        public TOutput ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target, out bool messageConsumed)
        {
            return ((ISourceBlock<TOutput>) _transformManyBlock).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public void Fault(Exception exception) { ((ISourceBlock<TOutput>) _transformManyBlock).Fault(exception); }

        public IDisposable LinkTo(ITargetBlock<TOutput> target, DataflowLinkOptions linkOptions)
        {
            return _transformManyBlock.LinkTo(target, linkOptions);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, TInput messageValue, ISourceBlock<TInput> source,
            bool consumeToAccept)
        {
            return ((ITargetBlock<TInput>) _transformManyBlock).OfferMessage(messageHeader, messageValue, source, consumeToAccept);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target)
        {
            ((ISourceBlock<TOutput>) _transformManyBlock).ReleaseReservation(messageHeader, target);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target)
        {
            return ((ISourceBlock<TOutput>) _transformManyBlock).ReserveMessage(messageHeader, target);
        }

        public bool TryReceive(Predicate<TOutput> filter, out TOutput item) { return _transformManyBlock.TryReceive(filter, out item); }

        public bool TryReceiveAll(out IList<TOutput> items) { return _transformManyBlock.TryReceiveAll(out items); }

        protected abstract IEnumerable<TOutput> Transform(TInput input);
    }
}