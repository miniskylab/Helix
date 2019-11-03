using System;

namespace Helix.Bot.Abstractions
{
    [Flags]
    public enum WorkflowState
    {
        None = 0,
        WaitingForActivation = 1 << 1,
        Activated = 1 << 2
    }
}