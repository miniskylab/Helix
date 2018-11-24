using Helix.Abstractions;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Helix.Implementations
{
    public class ViewModel
    {
        public int? BrokenUrlCount { [UsedImplicitly] get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public CrawlerState CrawlerState { [UsedImplicitly] get; set; }

        public string ElapsedTime { [UsedImplicitly] get; set; }

        public int? RemainingUrlCount { [UsedImplicitly] get; set; }

        public string StatusText { [UsedImplicitly] get; set; }

        public int? ValidUrlCount { [UsedImplicitly] get; set; }

        public int? VerifiedUrlCount { [UsedImplicitly] get; set; }
    }
}