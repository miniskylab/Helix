using System;

namespace Helix.Bot.Abstractions
{
    public interface IEventBroadcaster : IDisposable
    {
        event Action<Event> OnEventBroadcast;

        void Broadcast(Event @event);
    }
}