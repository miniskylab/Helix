using System;

namespace Helix.Crawler.Abstractions
{
    public interface IBrokenLinkCollectionWorkflow
    {
        event Action<Event> OnEventBroadcast;

        void SignalShutdown();

        bool TryActivate(string startUrl);

        void WaitForCompletion();
    }
}