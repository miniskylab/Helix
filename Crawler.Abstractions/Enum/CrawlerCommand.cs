namespace Helix.Crawler.Abstractions
{
    public enum CrawlerCommand
    {
        Activate,
        Run,
        Stop,
        Abort,
        MarkAsRanToCompletion,
        MarkAsCancelled,
        MarkAsFaulted,
        Pause,
        Resume
    }
}