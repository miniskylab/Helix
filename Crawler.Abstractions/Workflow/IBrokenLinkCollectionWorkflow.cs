using System;

namespace Helix.Crawler.Abstractions
{
    public interface IBrokenLinkCollectionWorkflow
    {
        int RemainingWorkload { get; }

        event Action<Event> OnEventBroadcast;

        void SignalShutdown();

        bool TryActivate(string startUrl);

        void WaitForCompletion();
    }
}