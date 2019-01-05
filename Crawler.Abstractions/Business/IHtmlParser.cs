namespace Helix.Crawler.Abstractions
{
    public interface IHtmlParser
    {
        event UrlCollectedEvent OnUrlCollected;

        void ExtractUrlsFrom(string html);
    }
}