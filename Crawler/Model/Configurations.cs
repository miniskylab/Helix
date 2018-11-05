using Newtonsoft.Json.Linq;

namespace CrawlerBackendBusiness
{
    public class Configurations
    {
        public bool EnableDebugMode { get; }
        public int MaxThreadCount { get; }
        public bool ReportBrokenLinksOnly { get; }
        public int RequestTimeoutDuration { get; }
        public string StartUrl { get; }
        public string TopLevelDomain { get; }
        public string UserAgent { get; }

        public Configurations(string configurationJsonString)
        {
            var tokens = JObject.Parse(configurationJsonString);
            EnableDebugMode = (bool) tokens.SelectToken(nameof(EnableDebugMode));
            MaxThreadCount = (int) tokens.SelectToken(nameof(MaxThreadCount));
            ReportBrokenLinksOnly = (bool) tokens.SelectToken(nameof(ReportBrokenLinksOnly));
            RequestTimeoutDuration = (int) tokens.SelectToken(nameof(RequestTimeoutDuration));
            StartUrl = ((string) tokens.SelectToken(nameof(StartUrl))).ToLower();
            UserAgent = (string) tokens.SelectToken(nameof(UserAgent));

            TopLevelDomain = (string) tokens.SelectToken(nameof(TopLevelDomain));
            if (string.IsNullOrWhiteSpace(TopLevelDomain)) TopLevelDomain = "_";
        }
    }
}