using System.Collections.Concurrent;

namespace Helix.Crawler.Abstractions
{
    public interface IBrokenLinkCollectionWorkflow
    {
        BlockingCollection<Event> Events { get; }

        int RemainingWorkload { get; }

        void SignalShutdown();

        bool TryActivate(string startUrl);

        void WaitForCompletion();
    }
}