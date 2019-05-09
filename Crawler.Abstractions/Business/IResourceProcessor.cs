namespace Helix.Crawler.Abstractions
{
    public interface IResourceProcessor
    {
        void Categorize(Resource resource, string contentType);

        Resource Enrich(Resource resource);
    }
}