namespace Helix.Bot.Abstractions
{
    public interface IStatistics
    {
        void DecrementRemainingWorkload();

        void DecrementValidUrlCountAndIncrementBrokenUrlCount();

        void IncrementBrokenUrlCount();

        void IncrementRemainingWorkload();

        void IncrementSuccessfullyRenderedPageCount(double millisecondsPageLoadTime);

        void IncrementValidUrlCount();

        void IncrementValidUrlCountAndDecrementBrokenUrlCount();

        StatisticsSnapshot TakeSnapshot();
    }
}