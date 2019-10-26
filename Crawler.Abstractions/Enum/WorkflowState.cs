using System;

namespace Helix.Crawler.Abstractions
{
    [Flags]
    public enum WorkflowState
    {
        None = 0,
        WaitingForActivation = 1 << 1,
        Activated = 1 << 2
    }
}