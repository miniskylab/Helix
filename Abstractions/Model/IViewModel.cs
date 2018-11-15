namespace Helix.Abstractions
{
    public interface IViewModel
    {
        int? BrokenUrlCount { get; set; }

        CrawlerState CrawlerState { get; set; }

        string ElapsedTime { get; set; }

        int? IdleWebBrowserCount { get; set; }

        int? RemainingUrlCount { get; set; }

        string StatusText { get; set; }

        int? ValidUrlCount { get; set; }

        int? VerifiedUrlCount { get; set; }
    }
}