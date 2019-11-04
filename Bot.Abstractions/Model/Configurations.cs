using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Helix.Bot.Abstractions
{
    public class Configurations
    {
        public static int GuiControllerPort { get; } = 18880;

        public TimeSpan HttpRequestTimeout { get; } = TimeSpan.FromMinutes(3);

        public int MaxHtmlRendererCount { get; } = 10;

        public int MaxNetworkConnectionCount { get; } = 2500;

        public static string PathToChromiumExecutable { get; } = Path.Combine(WorkingDirectory, "chromium/chrome.exe");

        public static string PathToDirectoryContainsScreenshotFiles { get; } = Path.Combine(WorkingDirectory, "screenshots");

        public static string PathToElectronJsExecutable { get; } = Path.Combine(WorkingDirectory, "ui/electron.exe");

        public static string PathToReportFile { get; } = Path.Combine(WorkingDirectory, "report.sqlite3");

        public static string PathToSqLiteBrowserExecutable { get; } =
            Path.Combine(WorkingDirectory, "sqlite-browser/DB Browser for SQLite.exe");

        public string RemoteHost { get; }

        public Uri StartUri { get; }

        public bool TakeScreenshotEvidence { get; } = true;

        public bool UseHeadlessWebBrowsers { get; } = true;

        public bool UseIncognitoWebBrowser { get; } = true;

        public bool VerifyExternalUrls { get; }

        public static string PathToLogFile => Path.Combine(WorkingDirectory, $"logs\\{nameof(Helix)}.{DateTime.Now:yyyyMMdd-HHmmss}.log");

        public static string WorkingDirectory => Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);

        public Configurations() { }

        public Configurations(string configurationJsonString)
        {
            var tokens = JObject.Parse(configurationJsonString);
            VerifyExternalUrls = (bool) (tokens.SelectToken(nameof(VerifyExternalUrls)) ?? false);
            StartUri = ValidateStartUri((string) tokens.SelectToken(nameof(StartUri)) ?? string.Empty);

            RemoteHost = ((string) tokens.SelectToken(nameof(RemoteHost)) ?? string.Empty).ToLower();
            if (string.IsNullOrWhiteSpace(RemoteHost)) RemoteHost = "_";
        }

        static Uri ValidateStartUri(string startUrl)
        {
            if (!Uri.TryCreate(startUrl, UriKind.Absolute, out var startUri))
                throw new UriFormatException("Invalid URI: The format of the [Start Uri] could not be determined.");
            return startUri;
        }
    }
}