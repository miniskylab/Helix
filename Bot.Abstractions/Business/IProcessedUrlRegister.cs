using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IProcessedUrlRegister : IService
    {
        bool IsRegistered(string url);

        bool IsSavedToReportFile(string url);

        void MarkAsSavedToReportFile(string url);

        bool TryRegister(string url);
    }
}