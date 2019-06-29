namespace Helix.Crawler.Abstractions
{
    public interface IResourceEnricher
    {
        Resource Enrich(Resource resource);
    }
}