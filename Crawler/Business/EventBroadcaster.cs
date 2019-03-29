using System;
using Helix.Crawler.Abstractions;
using JetBrains.Annotations;

namespace Helix.Crawler
{
    [UsedImplicitly]
    public class EventBroadcaster : IEventBroadcaster
    {
        public event Action<Event> OnEventBroadcast;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public void Broadcast(Event @event) { OnEventBroadcast?.Invoke(@event); }
    }
}