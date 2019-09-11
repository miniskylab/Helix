using System.Threading;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    internal class EventBroadcasterBlock : TransformBlock<Event, Event>
    {
        public EventBroadcasterBlock(CancellationToken cancellationToken) : base(cancellationToken) { }

        protected override Event Transform(Event @event) { return @event; }
    }
}