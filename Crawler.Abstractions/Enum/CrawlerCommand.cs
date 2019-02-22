namespace Helix.Crawler.Abstractions
{
    public enum CrawlerCommand
    {
        StartWorking,
        StopWorking,
        MarkAsRanToCompletion,
        MarkAsCancelled,
        MarkAsFaulted,
        Pause,
        Resume
    }
}