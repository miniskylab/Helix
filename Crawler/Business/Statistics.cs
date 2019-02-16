using System;
using System.Threading;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class Statistics : IStatistics
    {
        readonly object _averagePageLoadTimeCalculationSync = new object();
        int _brokenUrlCount;
        int _successfullyRenderedPageCount;
        double _totalPageLoadTime;
        int _validUrlCount;
        int _verifiedUrlCount;

        public double AveragePageLoadTime
        {
            get
            {
                lock (_averagePageLoadTimeCalculationSync)
                {
                    return SuccessfullyRenderedPageCount == 0 ? 0 : TotalPageLoadTime / SuccessfullyRenderedPageCount;
                }
            }
        }

        public int BrokenUrlCount
        {
            get => Interlocked.CompareExchange(ref _brokenUrlCount, 0, 0);
            set => Interlocked.Exchange(ref _brokenUrlCount, value);
        }

        public int SuccessfullyRenderedPageCount
        {
            get => Interlocked.CompareExchange(ref _successfullyRenderedPageCount, 0, 0);
            set
            {
                lock (_averagePageLoadTimeCalculationSync) Interlocked.Exchange(ref _successfullyRenderedPageCount, value);
            }
        }

        public double TotalPageLoadTime
        {
            get => Interlocked.CompareExchange(ref _totalPageLoadTime, 0, 0);
            set
            {
                lock (_averagePageLoadTimeCalculationSync) Interlocked.Exchange(ref _totalPageLoadTime, value);
            }
        }

        public int ValidUrlCount
        {
            get => Interlocked.CompareExchange(ref _validUrlCount, 0, 0);
            set => Interlocked.Exchange(ref _validUrlCount, value);
        }

        public int VerifiedUrlCount
        {
            get => Interlocked.CompareExchange(ref _verifiedUrlCount, 0, 0);
            set => Interlocked.Exchange(ref _verifiedUrlCount, value);
        }

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Statistics() { }
    }
}