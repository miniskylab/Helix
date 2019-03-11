namespace Helix.Crawler.Abstractions
{
    public interface IResourceProcessor
    {
        Resource Categorize(Resource resource, string contentType);

        Resource Enrich(Resource resource);
    }
}