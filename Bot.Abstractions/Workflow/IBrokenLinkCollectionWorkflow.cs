using System;

namespace Helix.Bot.Abstractions
{
    public interface IBrokenLinkCollectionWorkflow
    {
        event Action<Event> OnEventBroadcast;

        void Shutdown();

        bool TryActivate(string startUrl);
    }
}