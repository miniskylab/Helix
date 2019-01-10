using System;
using Newtonsoft.Json.Linq;

namespace Helix.Crawler.Abstractions
{
    public class Configurations
    {
        public string DomainName { get; }

        public int HttpProxyPort { get; set; }

        public bool ReportBrokenLinksOnly { get; }

        public int RequestTimeoutDuration { get; }

        public bool ShowWebBrowsers { get; }

        public Uri StartUri { get; }

        public bool UseIncognitoWebBrowser { get; }

        public int WebBrowserCount { get; }

        public Configurations(string domainName = "", bool reportBrokenLinksOnly = false, int requestTimeoutDuration = 0,
            bool showWebBrowsers = false, Uri startUri = null, int webBrowserCount = 0)
        {
            DomainName = domainName;
            ReportBrokenLinksOnly = reportBrokenLinksOnly;
            RequestTimeoutDuration = requestTimeoutDuration;
            ShowWebBrowsers = showWebBrowsers;
            StartUri = startUri;
            WebBrowserCount = webBrowserCount;
        }

        public Configurations(string configurationJsonString)
        {
            var tokens = JObject.Parse(configurationJsonString);
            ShowWebBrowsers = (bool) (tokens.SelectToken(nameof(ShowWebBrowsers)) ?? false);
            WebBrowserCount = (int) (tokens.SelectToken(nameof(WebBrowserCount)) ?? 0);
            ReportBrokenLinksOnly = (bool) (tokens.SelectToken(nameof(ReportBrokenLinksOnly)) ?? false);
            RequestTimeoutDuration = (int) (tokens.SelectToken(nameof(RequestTimeoutDuration)) ?? 0);
            StartUri = ValidateStartUri((string) tokens.SelectToken(nameof(StartUri)) ?? string.Empty);
            HttpProxyPort = 18882;
            UseIncognitoWebBrowser = true;

            DomainName = ((string) tokens.SelectToken(nameof(DomainName)) ?? string.Empty).ToLower();
            if (string.IsNullOrWhiteSpace(DomainName)) DomainName = "_";
        }

        static Uri ValidateStartUri(string startUrl)
        {
            if (!Uri.TryCreate(startUrl, UriKind.Absolute, out var startUri))
                throw new UriFormatException("Invalid URI: The format of the [Start Uri] could not be determined.");
            return startUri;
        }
    }
}