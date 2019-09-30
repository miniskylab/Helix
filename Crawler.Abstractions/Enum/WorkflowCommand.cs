namespace Helix.Crawler.Abstractions
{
    public enum WorkflowCommand
    {
        Initialize,
        Run,
        Abort,
        Stop,
        MarkAsRanToCompletion,
        MarkAsCancelled,
        MarkAsFaulted
    }
}