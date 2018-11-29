using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Helix.Abstractions
{
    public interface IMemory
    {
        bool AllBackgroundTasksAreDone { get; }

        IEnumerable<Task> BackgroundTasks { get; }

        CancellationToken CancellationToken { get; }

        CancellationTokenSource CancellationTokenSource { get; }

        Configurations Configurations { get; }

        CrawlerState CrawlerState { get; }

        string ErrorFilePath { get; }

        bool EverythingIsDone { get; }

        int RemainingUrlCount { get; }

        void Forget(Task backgroundTask);

        void ForgetAllBackgroundTasks();

        void Memorize(IRawResource toBeVerifiedRawResource);

        void Memorize(IResource toBeCrawledResource);

        void Memorize(Task backgroundTask);

        bool TryTakeToBeCrawledResource(out IResource toBeCrawledResource);

        bool TryTakeToBeVerifiedRawResource(out IRawResource toBeVerifiedRawResource);

        bool TryTransitTo(CrawlerState crawlerState);
    }
}