using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Helix.Crawler.Abstractions
{
    public class Configurations
    {
        public string DomainName { get; }

        public int HtmlRendererCount { get; }

        public string PathToDirectoryContainsScreenshotFiles { get; }

        public int RequestTimeoutDuration { get; }

        public Uri StartUri { get; }

        public bool TakeScreenshotEvidence { get; }

        public bool UseHeadlessWebBrowsers { get; }

        public bool UseIncognitoWebBrowser { get; }

        public string UserAgent { get; }

        public bool VerifyExternalUrls { get; }

        public Configurations(Uri startUri = null, string domainName = "", int htmlRendererCount = 4, int requestTimeoutDuration = 30,
            bool verifyExternalUrls = true, bool useHeadlessWebBrowsers = true, bool useIncognitoWebBrowser = true, string userAgent = "",
            bool takeScreenshotEvidence = false, string pathToDirectoryContainsScreenshotFiles = "")
        {
            StartUri = startUri;
            UserAgent = userAgent;
            DomainName = domainName;
            HtmlRendererCount = htmlRendererCount;
            VerifyExternalUrls = verifyExternalUrls;
            RequestTimeoutDuration = requestTimeoutDuration;
            UseHeadlessWebBrowsers = useHeadlessWebBrowsers;
            UseIncognitoWebBrowser = useIncognitoWebBrowser;
            TakeScreenshotEvidence = takeScreenshotEvidence;
            PathToDirectoryContainsScreenshotFiles = pathToDirectoryContainsScreenshotFiles;
        }

        public Configurations(string configurationJsonString)
        {
            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            if (workingDirectory == null) throw new InvalidOperationException();

            var tokens = JObject.Parse(configurationJsonString);
            UseHeadlessWebBrowsers = (bool) (tokens.SelectToken(nameof(UseHeadlessWebBrowsers)) ?? false);
            HtmlRendererCount = (int) (tokens.SelectToken(nameof(HtmlRendererCount)) ?? 0);
            VerifyExternalUrls = (bool) (tokens.SelectToken(nameof(VerifyExternalUrls)) ?? false);
            StartUri = ValidateStartUri((string) tokens.SelectToken(nameof(StartUri)) ?? string.Empty);
            PathToDirectoryContainsScreenshotFiles = Path.Combine(workingDirectory, "screenshots");
            TakeScreenshotEvidence = true;
            UseIncognitoWebBrowser = true;
            RequestTimeoutDuration = 30;
            UserAgent =
                "Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/72.0.3626.109 Safari/537.36";
            // TODO: add to Chromium

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