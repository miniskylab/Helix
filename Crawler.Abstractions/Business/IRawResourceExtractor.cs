using System;

namespace Helix.Crawler.Abstractions
{
    public interface IRawResourceExtractor
    {
        void ExtractRawResourcesFrom(HtmlDocument htmlDocument, Action<RawResource> onRawResourceExtracted);
    }
}