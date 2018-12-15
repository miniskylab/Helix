using Newtonsoft.Json.Linq;

namespace Helix.Crawler.Abstractions
{
    public class Configurations
    {
        public string DomainName { get; }

        public bool ReportBrokenLinksOnly { get; }

        public int RequestTimeoutDuration { get; }

        public bool ShowWebBrowsers { get; }

        public string StartUrl { get; }

        public int WebBrowserCount { get; }

        public Configurations(string domainName = "", bool reportBrokenLinksOnly = false, int requestTimeoutDuration = 0,
            bool showWebBrowsers = false, string startUrl = "", int webBrowserCount = 0)
        {
            DomainName = domainName;
            ReportBrokenLinksOnly = reportBrokenLinksOnly;
            RequestTimeoutDuration = requestTimeoutDuration;
            ShowWebBrowsers = showWebBrowsers;
            StartUrl = startUrl;
            WebBrowserCount = webBrowserCount;
        }

        public Configurations(string configurationJsonString)
        {
            var tokens = JObject.Parse(configurationJsonString);
            ShowWebBrowsers = (bool) (tokens.SelectToken(nameof(ShowWebBrowsers)) ?? false);
            WebBrowserCount = (int) (tokens.SelectToken(nameof(WebBrowserCount)) ?? 0);
            ReportBrokenLinksOnly = (bool) (tokens.SelectToken(nameof(ReportBrokenLinksOnly)) ?? false);
            RequestTimeoutDuration = (int) (tokens.SelectToken(nameof(RequestTimeoutDuration)) ?? 0);
            StartUrl = ((string) tokens.SelectToken(nameof(StartUrl)) ?? string.Empty).ToLower();

            DomainName = ((string) tokens.SelectToken(nameof(DomainName)) ?? string.Empty).ToLower();
            if (string.IsNullOrWhiteSpace(DomainName)) DomainName = "_";
        }
    }
}