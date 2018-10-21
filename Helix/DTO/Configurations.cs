using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Helix
{
    public static class Configurations
    {
        public static bool EnableDebugMode { get; }
        public static int MaxCrawlerCount { get; }
        public static bool ReportBrokenLinksOnly { get; }
        public static int RequestTimeoutDuration { get; }
        public static Uri StartUri { get; }
        public static string TopLevelDomain { get; }
        public static string UserAgent { get; }

        static Configurations()
        {
            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var tokens = JObject.Parse(File.ReadAllText($"{workingDirectory}/configurations.json"));
            EnableDebugMode = (bool) tokens.SelectToken(nameof(EnableDebugMode));
            MaxCrawlerCount = (int) tokens.SelectToken(nameof(MaxCrawlerCount));
            ReportBrokenLinksOnly = (bool) tokens.SelectToken(nameof(ReportBrokenLinksOnly));
            RequestTimeoutDuration = (int) tokens.SelectToken(nameof(RequestTimeoutDuration));
            StartUri = new Uri((string) tokens.SelectToken(nameof(StartUri)));
            UserAgent = (string) tokens.SelectToken(nameof(UserAgent));

            TopLevelDomain = (string) tokens.SelectToken(nameof(TopLevelDomain));
            if (string.IsNullOrWhiteSpace(TopLevelDomain)) TopLevelDomain = "_";
        }
    }
}