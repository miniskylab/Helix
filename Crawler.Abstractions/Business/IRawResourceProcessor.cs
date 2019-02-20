namespace Helix.Crawler.Abstractions
{
    public interface IRawResourceProcessor
    {
        void ProcessRawResource(RawResource rawResource, out Resource resource);
    }
}