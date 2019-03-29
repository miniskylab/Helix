namespace Helix.Crawler.Abstractions
{
    public interface IStatistics
    {
        int BrokenUrlCount { get; }
        double MillisecondsAveragePageLoadTime { get; }

        int ValidUrlCount { get; }

        int VerifiedUrlCount { get; }

        void IncrementBrokenUrlCount();

        void IncrementSuccessfullyRenderedPageCount();

        void IncrementTotalPageLoadTimeBy(double milliseconds);

        void IncrementValidUrlCount();
    }
}