using System;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using HtmlAgilityPackDocument = HtmlAgilityPack.HtmlDocument;

namespace Helix.Crawler
{
    public class RawResourceExtractor : IRawResourceExtractor
    {
        readonly Func<string, bool> _urlSchemeIsSupported = url => url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                                                                   url.StartsWith("https", StringComparison.OrdinalIgnoreCase) ||
                                                                   url.StartsWith("/", StringComparison.OrdinalIgnoreCase);
        public event RawResourceExtractedEvent OnRawResourceExtracted;

        public void ExtractRawResourcesFrom(HtmlDocument htmlDocument)
        {
            if (htmlDocument == null) throw new ArgumentNullException();
            var htmlAgilityPackDocument = new HtmlAgilityPackDocument();
            htmlAgilityPackDocument.LoadHtml(htmlDocument.Text);

            var anchorTags = htmlAgilityPackDocument.DocumentNode.SelectNodes("//a[@href]");
            if (anchorTags == null) return;
            Parallel.ForEach(anchorTags, anchorTag =>
            {
                var url = anchorTag.Attributes["href"].Value;
                if (_urlSchemeIsSupported(url)) OnRawResourceExtracted?.Invoke(new RawResource { ParentUrl = htmlDocument.Url, Url = url });
            });
        }
    }
}