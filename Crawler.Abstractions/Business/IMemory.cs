using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IMemory
    {
        CancellationToken CancellationToken { get; }

        Configurations Configurations { get; }

        CrawlerState CrawlerState { get; }

        string ErrorFilePath { get; }

        bool EverythingIsDone { get; }

        int RemainingUrlCount { get; }

        void CancelEverything();

        void DecrementActiveThreadCount();

        void IncrementActiveThreadCount();

        void Memorize(RawResource toBeVerifiedRawResource);

        void Memorize(Resource toBeCrawledResource);

        bool TryTakeToBeCrawledResource(out Resource toBeCrawledResource);

        bool TryTakeToBeVerifiedRawResource(out RawResource toBeVerifiedRawResource);

        bool TryTransitTo(CrawlerState crawlerState);
    }
}