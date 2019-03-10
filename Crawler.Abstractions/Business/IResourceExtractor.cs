using System;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceExtractor
    {
        void ExtractResourcesFrom(HtmlDocument htmlDocument, Action<Resource> onResourceExtracted);
    }
}