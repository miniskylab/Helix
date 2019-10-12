using System.Threading.Tasks.Dataflow;

namespace Helix.Crawler.Abstractions
{
    public interface ICoordinatorBlock : IPropagatorBlock<ProcessingResult, Resource>
    {
        BufferBlock<Event> Events { get; }

        void SignalShutdown();

        bool TryActivateWorkflow(string startUrl);
    }
}