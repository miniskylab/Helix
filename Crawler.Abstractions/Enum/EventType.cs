namespace Helix.Crawler.Abstractions
{
    public enum EventType
    {
        None,
        ResourceVerified,
        StartProgressUpdated,
        StopProgressUpdated,
        ReportFileCreated,
        Completed,
        NoMoreWorkToDo
    }
}