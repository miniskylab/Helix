using System;
using Newtonsoft.Json.Linq;

namespace Helix.Crawler.Abstractions
{
    public class Configurations
    {
        public string DomainName { get; }

        public int HtmlRendererCount { get; }

        public bool ReportBrokenLinksOnly { get; }

        public int RequestTimeoutDuration { get; }

        public Uri StartUri { get; }

        public bool UseHeadlessWebBrowsers { get; }

        public bool UseIncognitoWebBrowser { get; }

        public bool VerifyExternalUrls { get; }

        public Configurations(string domainName = "", bool reportBrokenLinksOnly = false, int requestTimeoutDuration = 0,
            bool useHeadlessWebBrowsers = false, Uri startUri = null, int htmlRendererCount = 0)
        {
            DomainName = domainName;
            ReportBrokenLinksOnly = reportBrokenLinksOnly;
            RequestTimeoutDuration = requestTimeoutDuration;
            UseHeadlessWebBrowsers = useHeadlessWebBrowsers;
            StartUri = startUri;
            HtmlRendererCount = htmlRendererCount;
        }

        public Configurations(string configurationJsonString)
        {
            var tokens = JObject.Parse(configurationJsonString);
            UseHeadlessWebBrowsers = (bool) (tokens.SelectToken(nameof(UseHeadlessWebBrowsers)) ?? false);
            HtmlRendererCount = (int) (tokens.SelectToken(nameof(HtmlRendererCount)) ?? 0);
            ReportBrokenLinksOnly = (bool) (tokens.SelectToken(nameof(ReportBrokenLinksOnly)) ?? false);
            RequestTimeoutDuration = (int) (tokens.SelectToken(nameof(RequestTimeoutDuration)) ?? 0);
            StartUri = ValidateStartUri((string) tokens.SelectToken(nameof(StartUri)) ?? string.Empty);
            UseIncognitoWebBrowser = true;
            VerifyExternalUrls = true;

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