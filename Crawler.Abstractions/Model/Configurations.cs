using System;
using Newtonsoft.Json.Linq;

namespace Helix.Crawler.Abstractions
{
    public class Configurations
    {
        public bool CaptureImageEvidence { get; }

        public string DomainName { get; }

        public int HtmlRendererCount { get; }

        public int RequestTimeoutDuration { get; }

        public Uri StartUri { get; }

        public bool UseHeadlessWebBrowsers { get; }

        public bool UseIncognitoWebBrowser { get; }

        public bool VerifyExternalUrls { get; }

        public Configurations(Uri startUri = null, string domainName = "", int htmlRendererCount = 4, int requestTimeoutDuration = 30,
            bool verifyExternalUrls = true, bool useHeadlessWebBrowsers = true, bool useIncognitoWebBrowser = true,
            bool captureImageEvidence = false)
        {
            StartUri = startUri;
            DomainName = domainName;
            HtmlRendererCount = htmlRendererCount;
            VerifyExternalUrls = verifyExternalUrls;
            RequestTimeoutDuration = requestTimeoutDuration;
            UseHeadlessWebBrowsers = useHeadlessWebBrowsers;
            UseIncognitoWebBrowser = useIncognitoWebBrowser;
            CaptureImageEvidence = captureImageEvidence;
        }

        public Configurations(string configurationJsonString)
        {
            var tokens = JObject.Parse(configurationJsonString);
            UseHeadlessWebBrowsers = (bool) (tokens.SelectToken(nameof(UseHeadlessWebBrowsers)) ?? false);
            HtmlRendererCount = (int) (tokens.SelectToken(nameof(HtmlRendererCount)) ?? 0);
            VerifyExternalUrls = (bool) (tokens.SelectToken(nameof(VerifyExternalUrls)) ?? false);
            StartUri = ValidateStartUri((string) tokens.SelectToken(nameof(StartUri)) ?? string.Empty);
            CaptureImageEvidence = true;
            UseIncognitoWebBrowser = true;
            RequestTimeoutDuration = 30;

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