using System.Threading;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    internal class EventBroadcasterBlock : TransformBlock<Event, Event>, IEventBroadcasterBlock
    {
        public EventBroadcasterBlock(CancellationToken cancellationToken) : base(cancellationToken, maxDegreeOfParallelism: 300) { }

        protected override Event Transform(Event @event) { return @event; }
    }
}