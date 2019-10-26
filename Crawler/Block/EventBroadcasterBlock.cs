using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class EventBroadcasterBlock : TransformBlock<Event, Event>, IEventBroadcasterBlock
    {
        protected override Event Transform(Event @event) { return @event; }
    }
}