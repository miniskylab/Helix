using System;
using JetBrains.Annotations;

namespace Helix.Bot.Abstractions
{
    [Flags]
    public enum BotState
    {
        [UsedImplicitly] None = 0,
        WaitingForInitialization = 1,
        WaitingToRun = 1 << 1,
        WaitingForStop = 1 << 2,
        Running = 1 << 3,
        RanToCompletion = 1 << 4,
        Cancelled = 1 << 5,
        Faulted = 1 << 6,
        Completed = RanToCompletion | Cancelled | Faulted
    }
}