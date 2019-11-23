using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IIncrementalIdGenerator : IService
    {
        int GetNext();
    }
}