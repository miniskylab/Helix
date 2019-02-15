﻿using System;
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
                if (!string.IsNullOrWhiteSpace(extractedUrl))
                    onRawResourceExtracted.Invoke(new RawResource
                    {
                        ParentUri = htmlDocument.Uri,
                        Url = extractedUrl
                    });
            });
        }
    }
}