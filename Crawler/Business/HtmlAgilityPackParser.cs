using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using HtmlAgilityPack;

namespace Helix.Crawler
{
    public class HtmlAgilityPackParser : IHtmlParser
    {
        public event UrlCollectedEvent OnUrlCollected;

        public void ExtractUrlsFrom(string html)
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var anchorTags = htmlDocument.DocumentNode.SelectNodes("//a[@href]");
            if (anchorTags == null) return;
            Parallel.ForEach(anchorTags, anchorTag => { OnUrlCollected?.Invoke(anchorTag.Attributes["href"].Value); });
        }
    }
}