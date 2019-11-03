namespace Helix.Bot.Abstractions
{
    public interface IResourceEnricher
    {
        Resource Enrich(Resource resource);
    }
}