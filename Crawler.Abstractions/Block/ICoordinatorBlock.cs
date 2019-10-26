using System.Threading.Tasks.Dataflow;

namespace Helix.Crawler.Abstractions
{
    public interface ICoordinatorBlock : IPropagatorBlock<ProcessingResult, Resource>, IReceivableSourceBlock<Resource>
    {
        BufferBlock<Event> Events { get; }

        int RemainingWorkload { get; }

        bool TryActivateWorkflow(string startUrl);
    }
}