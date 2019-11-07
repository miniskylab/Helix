namespace Helix.Bot.Abstractions
{
    public class ResourceVerifiedEvent : Event
    {
        public int BrokenUrlCount { get; set; }

        public int ValidUrlCount { get; set; }

        public int VerifiedUrlCount { get; set; }
    }
}