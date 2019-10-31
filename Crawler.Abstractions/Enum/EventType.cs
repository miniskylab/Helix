namespace Helix.Crawler.Abstractions
{
    public enum EventType
    {
        None,
        ResourceVerified,
        StartProgressUpdated,
        StopProgressUpdated,
        WorkflowActivated,
        Completed,
        NoMoreWorkToDo
    }
}