using System;

namespace Helix.Crawler.Abstractions
{
    [Flags]
    public enum CrawlerState
    {
        WaitingToRun = 1 << 0,
        Running = 1 << 1,
        Stopping = 1 << 2,
        RanToCompletion = 1 << 3,
        Cancelled = 1 << 4,
        Faulted = 1 << 5,
        Paused = 1 << 6,
        Completed = RanToCompletion | Cancelled | Faulted
    }
}