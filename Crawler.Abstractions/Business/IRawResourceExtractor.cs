using System;

namespace Helix.Crawler.Abstractions
{
    public interface IRawResourceExtractor
    {
        event IdleEvent OnIdle;

        void ExtractRawResourcesFrom(HtmlDocument htmlDocument, Action<RawResource> onRawResourceExtracted);
    }
}