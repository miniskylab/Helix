namespace Helix.Crawler.Abstractions
{
    public interface IRawResourceProcessor
    {
        bool TryProcessRawResource(RawResource rawResource, out Resource resource);
    }
}