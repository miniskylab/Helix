using System.Threading;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class EventBroadcasterBlock : TransformBlock<Event, Event>, IEventBroadcasterBlock
    {
        public EventBroadcasterBlock(CancellationToken cancellationToken) : base(cancellationToken) { }

        protected override Event Transform(Event @event) { return @event; }
    }
}