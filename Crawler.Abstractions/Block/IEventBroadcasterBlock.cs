using System.Threading.Tasks.Dataflow;

namespace Helix.Crawler.Abstractions
{
    public interface IEventBroadcasterBlock : IPropagatorBlock<Event, Event>, IReceivableSourceBlock<Event> { }
}