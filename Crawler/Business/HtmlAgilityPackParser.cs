using System;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using HtmlAgilityPack;

namespace Helix.Crawler
{
    public class HtmlAgilityPackParser : IHtmlParser
    {
        readonly Func<string, bool> _urlSchemeIsSupported = url => url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                                                                   url.StartsWith("https", StringComparison.OrdinalIgnoreCase) ||
                                                                   url.StartsWith("/", StringComparison.OrdinalIgnoreCase);
        public event UrlCollectedEvent OnUrlCollected;

        public void ExtractUrlsFrom(string html)
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var anchorTags = htmlDocument.DocumentNode.SelectNodes("//a[@href]");
            if (anchorTags == null) return;
            Parallel.ForEach(anchorTags, anchorTag =>
            {
                var url = anchorTag.Attributes["href"].Value;
                if (_urlSchemeIsSupported(url)) OnUrlCollected?.Invoke(url);
            });
        }
    }
}