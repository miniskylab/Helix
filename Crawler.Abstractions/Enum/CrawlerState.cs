using System;

namespace Helix.Crawler.Abstractions
{
    [Flags]
    public enum CrawlerState
    {
        None = 0,
        WaitingForInitialization = 1,
        WaitingToRun = 1 << 1,
        WaitingForStop = 1 << 2,
        Running = 1 << 3,
        RanToCompletion = 1 << 4,
        Cancelled = 1 << 5,
        Faulted = 1 << 6,
        Paused = 1 << 7,
        Completed = RanToCompletion | Cancelled | Faulted
    }
}