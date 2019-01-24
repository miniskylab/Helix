using System;

namespace Helix.Crawler.Abstractions
{
    public interface IRawResourceExtractor
    {
        event Action OnIdle;

        void ExtractRawResourcesFrom(HtmlDocument htmlDocument, Action<RawResource> onRawResourceExtracted);
    }
}