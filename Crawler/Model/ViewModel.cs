using Helix.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Helix.Implementations
{
    public class ViewModel : IViewModel
    {
        public int? BrokenUrlCount { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public CrawlerState CrawlerState { get; set; }

        public string ElapsedTime { get; set; }

        public int? IdleWebBrowserCount { get; set; }

        public int? RemainingUrlCount { get; set; }

        public string StatusText { get; set; }

        public int? ValidUrlCount { get; set; }

        public int? VerifiedUrlCount { get; set; }
    }
}