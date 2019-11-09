using System;
using JetBrains.Annotations;

namespace Helix.Bot.Abstractions
{
    [Flags]
    public enum WorkflowState
    {
        [UsedImplicitly] None = 0,
        WaitingForActivation = 1 << 1,
        Activated = 1 << 2
    }
}