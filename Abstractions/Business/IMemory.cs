using System.Threading;

namespace Helix.Abstractions
{
    public interface IMemory
    {
        CancellationToken CancellationToken { get; }

        CancellationTokenSource CancellationTokenSource { get; }

        Configurations Configurations { get; }

        CrawlerState CrawlerState { get; }

        string ErrorFilePath { get; }

        bool EverythingIsDone { get; }

        int RemainingUrlCount { get; }

        void DecrementActiveThreadCount();

        void IncrementActiveThreadCount();

        void Memorize(IRawResource toBeVerifiedRawResource);

        void Memorize(IResource toBeCrawledResource);

        bool TryTakeToBeCrawledResource(out IResource toBeCrawledResource);

        bool TryTakeToBeVerifiedRawResource(out IRawResource toBeVerifiedRawResource);

        bool TryTransitTo(CrawlerState crawlerState);
    }
}