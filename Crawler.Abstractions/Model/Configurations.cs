using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Helix.Crawler.Abstractions
{
    public class Configurations
    {
        public string DomainName { get; }

        public TimeSpan HttpRequestTimeout { get; } = TimeSpan.FromMinutes(3);

        public int MaxHtmlRendererCount { get; } = 10;

        public string PathToChromiumExecutable { get; } = Path.Combine(CurrentDirectory, "chromium/chrome.exe");

        public string PathToDirectoryContainsScreenshotFiles { get; } = Path.Combine(CurrentDirectory, "screenshots");

        public string PathToLogFile { get; } = Path.Combine(CurrentDirectory, "helix.log");

        public string PathToReportFile { get; } = Path.Combine(CurrentDirectory, "report.sqlite3");

        public int ResourceExtractorCount { get; } = 300;

        public int ResourceVerifierCount { get; } = 2500;

        public Uri StartUri { get; }

        public bool TakeScreenshotEvidence { get; } = true;

        public bool UseHeadlessWebBrowsers { get; }

        public bool UseIncognitoWebBrowser { get; } = true;

        public bool VerifyExternalUrls { get; }

        public string WorkingDirectory => CurrentDirectory;

        static string CurrentDirectory => Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        public Configurations() { }

        public Configurations(string configurationJsonString)
        {
            var tokens = JObject.Parse(configurationJsonString);
            UseHeadlessWebBrowsers = (bool) (tokens.SelectToken(nameof(UseHeadlessWebBrowsers)) ?? false);
            VerifyExternalUrls = (bool) (tokens.SelectToken(nameof(VerifyExternalUrls)) ?? false);
            StartUri = ValidateStartUri((string) tokens.SelectToken(nameof(StartUri)) ?? string.Empty);

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