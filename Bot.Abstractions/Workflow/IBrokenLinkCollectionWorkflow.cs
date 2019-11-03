using System.Collections.Concurrent;

namespace Helix.Bot.Abstractions
{
    public interface IBrokenLinkCollectionWorkflow
    {
        BlockingCollection<Event> Events { get; }

        void Shutdown();

        bool TryActivate(string startUrl);
    }
}