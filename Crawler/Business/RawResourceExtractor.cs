using System;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;
using HtmlAgilityPackDocument = HtmlAgilityPack.HtmlDocument;

namespace Helix.Crawler
{
    public class RawResourceExtractor : IRawResourceExtractor
    {
        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public RawResourceExtractor() { }

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
                var extractedUrl = anchorTag.Attributes["href"].Value;
                var absoluteUrl = EnsureAbsolute(extractedUrl, htmlDocument.Uri);
                if (uriSchemeIsSupported(new Uri(absoluteUrl, UriKind.Absolute)))
                    onRawResourceExtracted.Invoke(new RawResource
                    {
                        ParentUri = htmlDocument.Uri,
                        Url = absoluteUrl
                    });
            });

            string EnsureAbsolute(string relativeOrAbsoluteUrl, Uri parentUri)
            {
                var relativeOrAbsoluteUri = new Uri(relativeOrAbsoluteUrl, UriKind.RelativeOrAbsolute);
                if (relativeOrAbsoluteUri.IsAbsoluteUri) return relativeOrAbsoluteUrl;
                if (parentUri == null) throw new ArgumentException();

                var absoluteUri = new Uri(parentUri, relativeOrAbsoluteUrl);
                return relativeOrAbsoluteUrl.EndsWith("/") ? absoluteUri.AbsoluteUri : absoluteUri.AbsoluteUri.TrimEnd('/');
            }
            bool uriSchemeIsSupported(Uri uri) { return uri.Scheme == "http" || uri.Scheme == "https"; }
        }
    }
}