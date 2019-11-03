using Helix.Bot.Abstractions;

namespace Helix.Bot
{
    public class EventBroadcasterBlock : TransformBlock<Event, Event>, IEventBroadcasterBlock
    {
        protected override Event Transform(Event @event) { return @event; }
    }
}