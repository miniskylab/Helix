namespace Helix.Crawler.Abstractions
{
    public class Event
    {
        public EventType EventType { get; set; }

        public string Message { get; set; }
    }
}