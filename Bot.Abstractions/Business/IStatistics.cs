using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IStatistics : IService
    {
        void DecrementBrokenUrlCount();

        void DecrementRemainingWorkload();

        void DecrementValidUrlCount();

        void IncrementBrokenUrlCount();

        void IncrementRemainingWorkload();

        void IncrementSuccessfullyRenderedPageCount(double millisecondsPageLoadTime);

        void IncrementValidUrlCount();

        StatisticsSnapshot TakeSnapshot();
    }
}