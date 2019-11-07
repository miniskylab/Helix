namespace Helix.Bot.Abstractions
{
    public class ResourceProcessedEvent : Event
    {
        public int RemainingWorkload { get; set; }
    }
}