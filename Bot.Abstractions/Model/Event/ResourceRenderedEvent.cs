namespace Helix.Bot.Abstractions
{
    public class ResourceRenderedEvent : Event
    {
        public int BrokenUrlCount { get; set; }

        public double MillisecondsAveragePageLoadTime { get; set; }

        public int ValidUrlCount { get; set; }

        public int VerifiedUrlCount { get; set; }
    }
}