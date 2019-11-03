namespace Helix.Bot.Abstractions
{
    public interface IStatistics
    {
        void DecrementRemainingWorkload();

        void IncrementBrokenUrlCount();

        void IncrementRemainingWorkload();

        void IncrementSuccessfullyRenderedPageCount();

        void IncrementTotalPageLoadTimeBy(double milliseconds);

        void IncrementValidUrlCount();

        StatisticsSnapshot TakeSnapshot();
    }
}