namespace Helix.Crawler.Abstractions
{
    public enum CrawlerState
    {
        WaitingToRun,
        Running,
        Stopping,
        RanToCompletion,
        Cancelled,
        Faulted,
        Paused
    }
}