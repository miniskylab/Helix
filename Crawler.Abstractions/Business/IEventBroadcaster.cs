using System;

namespace Helix.Crawler.Abstractions
{
    public interface IEventBroadcaster : IDisposable
    {
        event Action<Event> OnEventBroadcast;

        void Broadcast(Event @event);
    }
}