namespace Helix.Crawler.Abstractions
{
    public enum CrawlerState
    {
        Unknown,
        Ready,
        Working,
        Stopping,
        Paused
    }
}