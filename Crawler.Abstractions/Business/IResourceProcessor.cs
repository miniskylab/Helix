namespace Helix.Crawler.Abstractions
{
    public interface IResourceProcessor
    {
        bool TryProcessRawResource(RawResource rawResource, out Resource resource);
    }
}