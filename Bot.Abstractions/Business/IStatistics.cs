using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IStatistics : IService
    {
        void DecrementRemainingWorkload();

        void IncrementBrokenUrlCount();

        void IncrementRemainingWorkload();

        void IncrementSuccessfullyRenderedPageCount(double millisecondsPageLoadTime);

        void IncrementValidUrlCount();

        StatisticsSnapshot TakeSnapshot();
    }
}