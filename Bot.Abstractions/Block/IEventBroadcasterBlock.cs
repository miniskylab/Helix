using System.Threading.Tasks.Dataflow;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IEventBroadcasterBlock : IPropagatorBlock<Event, Event>, IReceivableSourceBlock<Event>, IService
    {
        int InputCount { get; }

        int OutputCount { get; }
    }
}