namespace Helix.Crawler.Abstractions
{
    public interface IRawResourceExtractor
    {
        event RawResourceExtractedEvent OnRawResourceExtracted;

        void ExtractRawResourcesFrom(HtmlDocument htmlDocument);
    }
}