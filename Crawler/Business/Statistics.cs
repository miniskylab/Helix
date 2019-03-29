using System;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class Statistics : IStatistics
    {
        readonly object _averagePageLoadTimeCalculationLock = new object();
        double _millisecondsTotalPageLoadTime;
        int _successfullyRenderedPageCount;
        readonly object _urlCountLock = new object();

        public int BrokenUrlCount { get; private set; }

        public int ValidUrlCount { get; private set; }

        public double MillisecondsAveragePageLoadTime
        {
            get
            {
                lock (_averagePageLoadTimeCalculationLock)
                {
                    return _successfullyRenderedPageCount == 0 ? 0 : _millisecondsTotalPageLoadTime / _successfullyRenderedPageCount;
                }
            }
        }

        public int VerifiedUrlCount
        {
            get
            {
                lock (_urlCountLock) return ValidUrlCount + BrokenUrlCount;
            }
        }

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Statistics() { }

        public void IncrementBrokenUrlCount()
        {
            lock (_urlCountLock) BrokenUrlCount++;
        }

        public void IncrementSuccessfullyRenderedPageCount()
        {
            lock (_averagePageLoadTimeCalculationLock) _successfullyRenderedPageCount++;
        }

        public void IncrementTotalPageLoadTimeBy(double milliseconds)
        {
            lock (_averagePageLoadTimeCalculationLock) _millisecondsTotalPageLoadTime += milliseconds;
        }

        public void IncrementValidUrlCount()
        {
            lock (_urlCountLock) ValidUrlCount++;
        }
    }
}