namespace Helix.Crawler.Abstractions
{
    public interface IContentTypeToResourceTypeDictionary
    {
        ResourceType this[string key] { get; }
    }
}