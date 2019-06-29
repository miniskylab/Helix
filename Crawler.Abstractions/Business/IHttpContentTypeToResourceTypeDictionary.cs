namespace Helix.Crawler.Abstractions
{
    public interface IHttpContentTypeToResourceTypeDictionary
    {
        ResourceType this[string httpContentType] { get; }
    }
}