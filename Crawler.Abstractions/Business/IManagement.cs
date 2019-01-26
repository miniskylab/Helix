using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IManagement
    {
        CancellationToken CancellationToken { get; }

        CrawlerState CrawlerState { get; }

        bool EverythingIsDone { get; }

        int RemainingUrlCount { get; }

        void CancelEverything();

        void InterlockedDecrementActiveExtractionThreadCount();

        void InterlockedDecrementActiveRenderingThreadCount();

        void InterlockedDecrementActiveVerificationThreadCount();

        void InterlockedIncrementActiveExtractionThreadCount();

        void InterlockedIncrementActiveRenderingThreadCount();

        void InterlockedIncrementActiveVerificationThreadCount();

        bool TryTransitTo(CrawlerState crawlerState);
    }
}