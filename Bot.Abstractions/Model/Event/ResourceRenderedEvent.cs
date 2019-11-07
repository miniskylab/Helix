namespace Helix.Bot.Abstractions
{
    public class ResourceRenderedEvent : Event
    {
        public double MillisecondsAveragePageLoadTime { get; set; }
    }
}