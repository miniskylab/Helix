using System;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class EventBroadcaster : IEventBroadcaster
    {
        public event Action<Event> OnEventBroadcast;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public void Broadcast(Event @event) { OnEventBroadcast?.Invoke(@event); }
    }
}