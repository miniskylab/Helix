using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IHttpContentTypeToResourceTypeDictionary : IService
    {
        ResourceType this[string httpContentType] { get; }
    }
}