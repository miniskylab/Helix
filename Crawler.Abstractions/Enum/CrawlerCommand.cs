namespace Helix.Crawler.Abstractions
{
    public enum CrawlerCommand
    {
        Initialize,
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