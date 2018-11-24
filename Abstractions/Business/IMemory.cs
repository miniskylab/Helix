using System.Threading;
using System.Threading.Tasks;

namespace Helix.Abstractions
{
    public interface IMemory
    {
        Task AllBackgroundCrawlingTasks { get; }

        CancellationToken CancellationToken { get; }

        CancellationTokenSource CancellationTokenSource { get; }

        Configurations Configurations { get; }

        CrawlerState CrawlerState { get; }

        string ErrorFilePath { get; }

        bool IsAllWorkDone { get; }

        int RemainingUrlCount { get; }

        void Forget(Task backgroundCrawlingTask);

        void ForgetAllBackgroundCrawlingTasks();

        void Memorize(IRawResource rawResource);

        void Memorize(Task backgroundCrawlingTask);

        bool TryTakeToBeVerifiedRawResource(out IRawResource rawResource);

        bool TryTransitTo(CrawlerState crawlerState);
    }
}