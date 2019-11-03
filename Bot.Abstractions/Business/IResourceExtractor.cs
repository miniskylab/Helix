using System.Collections.ObjectModel;

namespace Helix.Bot.Abstractions
{
    public interface IResourceExtractor
    {
        ReadOnlyCollection<Resource> ExtractResourcesFrom(HtmlDocument htmlDocument);
    }
}