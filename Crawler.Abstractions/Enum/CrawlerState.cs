using System;

namespace Helix.Crawler.Abstractions
{
    [Flags]
    public enum CrawlerState
    {
        None = 0,
        WaitingToRun = 1 << 0,
        Running = 1 << 1,
        RanToCompletion = 1 << 2,
        Cancelled = 1 << 3,
        Faulted = 1 << 4,
        Paused = 1 << 5,
        Completed = RanToCompletion | Cancelled | Faulted
    }
}