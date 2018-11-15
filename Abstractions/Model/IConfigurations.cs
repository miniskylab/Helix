namespace Helix.Abstractions
{
    public interface IConfigurations
    {
        string DomainName { get; }

        bool ReportBrokenLinksOnly { get; }

        int RequestTimeoutDuration { get; }

        bool ShowWebBrowsers { get; }

        string StartUrl { get; }

        string UserAgent { get; }

        int WebBrowserCount { get; }
    }
}