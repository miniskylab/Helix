using System.Collections.Concurrent;

namespace Helix.Crawler.Abstractions
{
    public interface IBrokenLinkCollectionWorkflow
    {
        BlockingCollection<Event> Events { get; }

        int RemainingWorkload { get; }

        void Shutdown();

        bool TryActivate(string startUrl);
    }
}