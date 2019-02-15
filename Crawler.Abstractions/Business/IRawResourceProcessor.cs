namespace Helix.Crawler.Abstractions
{
    public interface IRawResourceProcessor
    {
        HttpStatusCode TryProcessRawResource(RawResource rawResource, out Resource resource);
    }
}