using System.Collections.ObjectModel;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceExtractor
    {
        ReadOnlyCollection<Resource> ExtractResourcesFrom(HtmlDocument htmlDocument);
    }
}