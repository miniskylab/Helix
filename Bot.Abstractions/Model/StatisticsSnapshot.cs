namespace Helix.Bot.Abstractions
{
    public class StatisticsSnapshot
    {
        public int BrokenUrlCount { get; }

        public double MillisecondsAveragePageLoadTime { get; }

        public int RemainingWorkload { get; }

        public int ValidUrlCount { get; }

        public int VerifiedUrlCount { get; }

        public StatisticsSnapshot(int brokenUrlCount, int remainingWorkload, int validUrlCount, int verifiedUrlCount,
            double millisecondsAveragePageLoadTime)
        {
            ValidUrlCount = validUrlCount;
            BrokenUrlCount = brokenUrlCount;
            VerifiedUrlCount = verifiedUrlCount;
            RemainingWorkload = remainingWorkload;
            MillisecondsAveragePageLoadTime = millisecondsAveragePageLoadTime;
        }
    }
}