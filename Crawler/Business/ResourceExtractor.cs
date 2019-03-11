using System;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using HtmlAgilityPackDocument = HtmlAgilityPack.HtmlDocument;

namespace Helix.Crawler
{
    public class ResourceExtractor : IResourceExtractor
    {
        readonly IResourceProcessor _resourceProcessor;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ResourceExtractor(IResourceProcessor resourceProcessor) { _resourceProcessor = resourceProcessor; }

        public void ExtractResourcesFrom(HtmlDocument htmlDocument, Action<Resource> onResourceExtracted)
        {
            if (htmlDocument == null) throw new ArgumentNullException(nameof(htmlDocument));
            if (onResourceExtracted == null) throw new ArgumentNullException(nameof(onResourceExtracted));

            var htmlAgilityPackDocument = new HtmlAgilityPackDocument();
            htmlAgilityPackDocument.LoadHtml(htmlDocument.Text);

            var anchorTags = htmlAgilityPackDocument.DocumentNode.SelectNodes("//a[@href]");
            if (anchorTags == null) return;
            Parallel.ForEach(anchorTags, anchorTag =>
            {
                var extractedUrl = anchorTag.Attributes["href"].Value;
                if (IsNullOrWhiteSpace() || IsJavaScriptCode()) return;
                onResourceExtracted.Invoke(
                    _resourceProcessor.Enrich(new Resource
                    {
                        ParentUri = htmlDocument.Uri,
                        OriginalUrl = extractedUrl,
                        IsExtracted = true
                    })
                );

                bool IsNullOrWhiteSpace() { return string.IsNullOrWhiteSpace(extractedUrl); }
                bool IsJavaScriptCode() { return extractedUrl.StartsWith("javascript:", StringComparison.InvariantCultureIgnoreCase); }
            });
        }
    }
}