using System;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class Statistics : IStatistics
    {
        readonly object _averagePageLoadTimeCalculationLock = new object();
        int _brokenUrlCount;
        int _successfullyRenderedPageCount;
        double _totalPageLoadTime;
        int _validUrlCount;
        readonly object _verifiedUrlCalculationLock = new object();

        public double AveragePageLoadTime
        {
            get
            {
                lock (_averagePageLoadTimeCalculationLock)
                {
                    return SuccessfullyRenderedPageCount == 0 ? 0 : TotalPageLoadTime / SuccessfullyRenderedPageCount;
                }
            }
        }

        public int BrokenUrlCount
        {
            get
            {
                lock (_verifiedUrlCalculationLock) return _brokenUrlCount;
            }
            set
            {
                lock (_verifiedUrlCalculationLock) _brokenUrlCount = value;
            }
        }

        public int SuccessfullyRenderedPageCount
        {
            get
            {
                lock (_averagePageLoadTimeCalculationLock) return _successfullyRenderedPageCount;
            }
            set
            {
                lock (_averagePageLoadTimeCalculationLock) _successfullyRenderedPageCount = value;
            }
        }

        public double TotalPageLoadTime
        {
            get
            {
                lock (_averagePageLoadTimeCalculationLock) return _totalPageLoadTime;
            }
            set
            {
                lock (_averagePageLoadTimeCalculationLock) _totalPageLoadTime = value;
            }
        }

        public int ValidUrlCount
        {
            get
            {
                lock (_verifiedUrlCalculationLock) return _validUrlCount;
            }
            set
            {
                lock (_verifiedUrlCalculationLock) _validUrlCount = value;
            }
        }

        public int VerifiedUrlCount
        {
            get
            {
                lock (_verifiedUrlCalculationLock) return _validUrlCount + _brokenUrlCount;
            }
        }

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Statistics() { }
    }
}