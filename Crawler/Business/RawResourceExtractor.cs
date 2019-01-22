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

        public event IdleEvent OnIdle;

        public void ExtractRawResourcesFrom(HtmlDocument htmlDocument, Action<RawResource> onRawResourceExtracted)
        {
            if (htmlDocument == null) throw new ArgumentNullException(nameof(htmlDocument));
            if (onRawResourceExtracted == null) throw new ArgumentNullException(nameof(onRawResourceExtracted));
            var htmlAgilityPackDocument = new HtmlAgilityPackDocument();
            htmlAgilityPackDocument.LoadHtml(htmlDocument.Text);

            var anchorTags = htmlAgilityPackDocument.DocumentNode.SelectNodes("//a[@href]");
            if (anchorTags == null) return;
            Parallel.ForEach(anchorTags, anchorTag =>
            {
                var url = anchorTag.Attributes["href"].Value;
                if (_urlSchemeIsSupported(url))
                    onRawResourceExtracted?.Invoke(new RawResource
                    {
                        ParentUri = htmlDocument.Uri,
                        Url = EnsureAbsolute(url, htmlDocument.Uri)
                    });
            });
            OnIdle?.Invoke();
        }

        static string EnsureAbsolute(string possiblyRelativeUrl, Uri parentUri)
        {
            if (!possiblyRelativeUrl.StartsWith("/")) return possiblyRelativeUrl;
            if (parentUri == null) throw new ArgumentException();

            string baseString;
            if (possiblyRelativeUrl.StartsWith("//")) baseString = $"{parentUri.Scheme}:";
            else
            {
                baseString = $"{parentUri.Scheme}://{parentUri.Host}";
                if (!parentUri.IsDefaultPort) baseString += $":{parentUri.Port}";
            }
            return $"{baseString}{possiblyRelativeUrl}";
        }
    }
}