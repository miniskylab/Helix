namespace Helix.Bot.Abstractions
{
    public class RedirectHappenedAtStartUrlEvent : Event
    {
        public string FinalUrlAfterRedirects { get; set; }
    }
}