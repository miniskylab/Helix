using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IResourceEnricher : IService
    {
        Resource Enrich(Resource resource);
    }
}