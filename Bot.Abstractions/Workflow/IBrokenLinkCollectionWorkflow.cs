using System;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IBrokenLinkCollectionWorkflow : IService
    {
        event Action<Event> OnEventBroadcast;

        void Shutdown();

        bool TryActivate(string startUrl);
    }
}