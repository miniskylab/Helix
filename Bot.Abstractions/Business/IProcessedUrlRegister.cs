using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IProcessedUrlRegister : IService
    {
        bool IsRegistered(string url);

        bool TryRegister(string url);
    }
}