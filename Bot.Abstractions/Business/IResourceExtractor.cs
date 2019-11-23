using System.Collections.ObjectModel;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IResourceExtractor : IService
    {
        ReadOnlyCollection<Resource> ExtractResourcesFrom(HtmlDocument htmlDocument);
    }
}