namespace Helix.Bot.Abstractions
{
    public class AveragePageLoadTimeUpdatedEvent : Event
    {
        public double MillisecondsAveragePageLoadTime { get; set; }
    }
}