using System;

namespace Helix.Crawler.Abstractions
{
    public interface IRawResourceExtractor
    {
        event IdleEvent OnIdle;
        event Action<RawResource> OnRawResourceExtracted;

        void ExtractRawResourcesFrom(HtmlDocument htmlDocument);
    }
}