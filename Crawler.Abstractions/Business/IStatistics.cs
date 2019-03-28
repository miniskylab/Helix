namespace Helix.Crawler.Abstractions
{
    public interface IStatistics
    {
        double AveragePageLoadTime { get; }

        int BrokenUrlCount { get; set; }

        int SuccessfullyRenderedPageCount { get; set; }

        double TotalPageLoadTime { get; set; }

        int ValidUrlCount { get; set; }

        int VerifiedUrlCount { get; }
    }
}