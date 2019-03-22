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

        public string PathToChromiumExecutable { get; }

        public string PathToDirectoryContainsScreenshotFiles { get; }

        public Uri StartUri { get; }

        public bool TakeScreenshotEvidence { get; }

        public bool UseHeadlessWebBrowsers { get; }

        public bool UseIncognitoWebBrowser { get; }

        public string UserAgent { get; set; }

        public bool VerifyExternalUrls { get; }

        public string WorkingDirectory { get; }

        public Configurations(Uri startUri = null, string domainName = "", int htmlRendererCount = 4, bool verifyExternalUrls = true,
            bool useHeadlessWebBrowsers = true, bool useIncognitoWebBrowser = true, bool takeScreenshotEvidence = false,
            string pathToDirectoryContainsScreenshotFiles = "", string pathToChromiumExecutable = "")
        {
            StartUri = startUri;
            DomainName = domainName;
            HtmlRendererCount = htmlRendererCount;
            VerifyExternalUrls = verifyExternalUrls;
            UseHeadlessWebBrowsers = useHeadlessWebBrowsers;
            UseIncognitoWebBrowser = useIncognitoWebBrowser;
            TakeScreenshotEvidence = takeScreenshotEvidence;
            PathToChromiumExecutable = pathToChromiumExecutable;
            PathToDirectoryContainsScreenshotFiles = pathToDirectoryContainsScreenshotFiles;
        }

        public Configurations(string configurationJsonString)
        {
            WorkingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            if (WorkingDirectory == null) throw new InvalidOperationException();

            var tokens = JObject.Parse(configurationJsonString);
            UseHeadlessWebBrowsers = (bool) (tokens.SelectToken(nameof(UseHeadlessWebBrowsers)) ?? false);
            HtmlRendererCount = (int) (tokens.SelectToken(nameof(HtmlRendererCount)) ?? 0);
            VerifyExternalUrls = (bool) (tokens.SelectToken(nameof(VerifyExternalUrls)) ?? false);
            StartUri = ValidateStartUri((string) tokens.SelectToken(nameof(StartUri)) ?? string.Empty);
            PathToDirectoryContainsScreenshotFiles = Path.Combine(WorkingDirectory, "screenshots");
            PathToChromiumExecutable = Path.Combine(WorkingDirectory, "chromium/chrome.exe");
            TakeScreenshotEvidence = true;
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