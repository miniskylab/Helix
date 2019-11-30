using System;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IBrokenLinkCollectionWorkflow : IService, IWorkflow
    {
        event Action<Event> OnEventBroadcast;

        void Shutdown();

        bool TryActivate(string startUrl);
    }
}