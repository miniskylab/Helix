using System;

namespace Helix.Crawler.Abstractions
{
    [Flags]
    public enum CrawlerState
    {
        None = 0,
        WaitingForActivation = 1 << 0,
        WaitingToRun = 1 << 1,
        Running = 1 << 2,
        RanToCompletion = 1 << 3,
        Cancelled = 1 << 4,
        Faulted = 1 << 5,
        Paused = 1 << 6,
        Completed = RanToCompletion | Cancelled | Faulted
    }
}