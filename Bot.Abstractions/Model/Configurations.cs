using System;
using System.Data;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Helix.Bot.Abstractions
{
    public class Configurations
    {
        readonly string _id;

        public bool IncludeNonHttpUrlsInReport { get; }

        public bool IncludeRedirectUrlsInReport { get; }

        public string RemoteHost { get; }

        public Uri StartUri { get; }

        public bool TakeScreenshotEvidence { get; } = true;

        public bool UseHeadlessWebBrowsers { get; } = true;

        public bool UseIncognitoWebBrowser { get; } = true;

        public static int GuiControllerPort => 18880;

        public static TimeSpan HttpRequestTimeout => TimeSpan.FromMinutes(3);

        public static int MaxHtmlRendererCount => 10;

        public static int MaxNetworkConnectionCount => 300;

        public static string PathToChromiumExecutable => Path.Combine(WorkingDirectory, "chromium/chrome.exe");

        public string PathToDirectoryContainsScreenshotFiles => Path.Combine(OutputDirectory, $"{_id}/Screenshots");

        public static string PathToElectronJsExecutable => Path.Combine(WorkingDirectory, "ui/electron.exe");

        public string PathToLogFile => Path.Combine(OutputDirectory, $"{_id}/Helix.log");

        public string PathToReportFile => Path.Combine(OutputDirectory, $"{_id}/Report.sqlite3");

        public static string PathToSqLiteBrowserExecutable => Path.Combine(WorkingDirectory, "sqlite-browser/DB Browser for SQLite.exe");

        public static int RenderableResourceSizeInMb => 10;

        public static string WorkingDirectory => Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);

        static string OutputDirectory => Path.Combine(WorkingDirectory, "_outputs");

        public Configurations() { }

        public Configurations(string configurationJsonString)
        {
            var tokens = JObject.Parse(configurationJsonString);
            StartUri = ValidateStartUri((string) tokens.SelectToken(nameof(StartUri)) ?? string.Empty);
            IncludeNonHttpUrlsInReport = (bool) (tokens.SelectToken(nameof(IncludeNonHttpUrlsInReport)) ?? false);
            IncludeRedirectUrlsInReport = (bool) (tokens.SelectToken(nameof(IncludeRedirectUrlsInReport)) ?? false);

            RemoteHost = ((string) tokens.SelectToken(nameof(RemoteHost)) ?? string.Empty).ToLower();
            if (string.IsNullOrWhiteSpace(RemoteHost)) RemoteHost = "_";

            _id = $"{StartUri.Host}-{StartUri.Port}-{DateTime.Now:yyyyMMdd-HHmmss}";

            if (Directory.Exists(Path.Combine(OutputDirectory, $"{_id}")))
                throw new InvalidConstraintException($"{nameof(Configurations)} ID collision detected.");
            Directory.CreateDirectory(Path.Combine(OutputDirectory, $"{_id}"));
        }

        static Uri ValidateStartUri(string startUrl)
        {
            if (!Uri.TryCreate(startUrl, UriKind.Absolute, out var startUri))
                throw new UriFormatException("Invalid URI: The format of the [Start Uri] could not be determined.");
            return startUri;
        }
    }
}