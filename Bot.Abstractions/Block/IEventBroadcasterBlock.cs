using System.Threading.Tasks.Dataflow;

namespace Helix.Bot.Abstractions
{
    public interface IEventBroadcasterBlock : IPropagatorBlock<Event, Event>, IReceivableSourceBlock<Event> { }
}