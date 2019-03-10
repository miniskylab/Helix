namespace Helix.Crawler.Abstractions
{
    public interface IResourceProcessor
    {
        Resource Enrich(Resource resource);
    }
}