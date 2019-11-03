namespace Helix.Bot.Abstractions
{
    public interface IHttpContentTypeToResourceTypeDictionary
    {
        ResourceType this[string httpContentType] { get; }
    }
}