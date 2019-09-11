using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Helix.Crawler.Abstractions;
using HtmlAgilityPackDocument = HtmlAgilityPack.HtmlDocument;

namespace Helix.Crawler
{
    public class ResourceExtractor : IResourceExtractor
    {
        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ResourceExtractor() { }

        public ReadOnlyCollection<Resource> ExtractResourcesFrom(HtmlDocument htmlDocument)
        {
            if (htmlDocument == null) throw new ArgumentNullException(nameof(htmlDocument));

            var htmlAgilityPackDocument = new HtmlAgilityPackDocument();
            htmlAgilityPackDocument.LoadHtml(htmlDocument.Text);

            var extractedResources = new List<Resource>();
            var anchorTags = htmlAgilityPackDocument.DocumentNode.SelectNodes("//a[@href]");
            if (anchorTags == null) return new ReadOnlyCollection<Resource>(extractedResources);

            foreach (var anchorTag in anchorTags)
            {
                var extractedUrl = anchorTag.Attributes["href"].Value;
                if (IsNullOrWhiteSpace() || IsJavaScriptCode()) continue;
                extractedResources.Add(
                    new Resource
                    {
                        ParentUri = htmlDocument.Uri,
                        OriginalUrl = extractedUrl,
                        IsExtracted = true
                    }
                );

                bool IsNullOrWhiteSpace() { return string.IsNullOrWhiteSpace(extractedUrl); }
                bool IsJavaScriptCode() { return extractedUrl.StartsWith("javascript:", StringComparison.InvariantCultureIgnoreCase); }
            }

            return new ReadOnlyCollection<Resource>(extractedResources);
        }
    }
}