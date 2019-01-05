using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class HtmlAgilityPackParser : IHtmlParser
    {
        public event UrlCollectedEvent OnUrlCollected;

        public void ExtractUrlsFrom(string html)
        {
            OnUrlCollected?.Invoke("");
            throw new System.NotImplementedException();
        }
    }
}