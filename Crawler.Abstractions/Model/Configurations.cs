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

        public Configurations(string configurationJsonString)
        {
            var tokens = JObject.Parse(configurationJsonString);
            ShowWebBrowsers = (bool) tokens.SelectToken(nameof(ShowWebBrowsers));
            WebBrowserCount = (int) tokens.SelectToken(nameof(WebBrowserCount));
            ReportBrokenLinksOnly = (bool) tokens.SelectToken(nameof(ReportBrokenLinksOnly));
            RequestTimeoutDuration = (int) tokens.SelectToken(nameof(RequestTimeoutDuration));
            StartUrl = ((string) tokens.SelectToken(nameof(StartUrl))).ToLower();

            DomainName = (string) tokens.SelectToken(nameof(DomainName));
            if (string.IsNullOrWhiteSpace(DomainName)) DomainName = "_";
        }
    }
}