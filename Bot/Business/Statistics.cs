using System;
using Helix.Bot.Abstractions;

namespace Helix.Bot
{
    public class Statistics : IStatistics
    {
        int _brokenUrlCount;
        double _millisecondsTotalPageLoadTime;
        int _remainingWorkload;
        int _successfullyRenderedPageCount;
        int _validUrlCount;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Statistics() { }

        public void DecrementRemainingWorkload()
        {
            lock (_remainingWorkloadCalculationLock) _remainingWorkload--;
        }

        public void IncrementBrokenUrlCount()
        {
            lock (_urlCountLock) _brokenUrlCount++;
        }

        public void IncrementRemainingWorkload()
        {
            lock (_remainingWorkloadCalculationLock) _remainingWorkload++;
        }

        public void IncrementSuccessfullyRenderedPageCount(double millisecondsPageLoadTime)
        {
            lock (_averagePageLoadTimeCalculationLock)
            {
                _successfullyRenderedPageCount++;
                _millisecondsTotalPageLoadTime += millisecondsPageLoadTime;
            }
        }

        public void IncrementValidUrlCount()
        {
            lock (_urlCountLock) _validUrlCount++;
        }

        public StatisticsSnapshot TakeSnapshot()
        {
            lock (_urlCountLock)
            lock (_remainingWorkloadCalculationLock)
            lock (_averagePageLoadTimeCalculationLock)
            {
                var verifiedUrlCount = _validUrlCount + _brokenUrlCount;
                var millisecondsAveragePageLoadTime = _successfullyRenderedPageCount != 0
                    ? _millisecondsTotalPageLoadTime / _successfullyRenderedPageCount
                    : 0;

                return new StatisticsSnapshot(
                    _brokenUrlCount,
                    _remainingWorkload,
                    _validUrlCount,
                    verifiedUrlCount,
                    millisecondsAveragePageLoadTime
                );
            }
        }

        #region Calculation Locks

        readonly object _averagePageLoadTimeCalculationLock = new object();
        readonly object _remainingWorkloadCalculationLock = new object();
        readonly object _urlCountLock = new object();

        #endregion
    }
}