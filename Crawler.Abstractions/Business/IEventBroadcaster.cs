using System;

namespace Helix.Crawler.Abstractions
{
    public interface IEventBroadcaster
    {
        event Action<Event> OnEventBroadcast;

        void Broadcast(Event @event);
    }
}