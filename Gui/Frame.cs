using Helix.Crawler.Abstractions;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Helix.Gui
{
    public class Frame
    {
        public int? BrokenUrlCount { [UsedImplicitly] get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public CrawlerState CrawlerState { [UsedImplicitly] get; set; }

        public string ElapsedTime { [UsedImplicitly] get; set; }

        public double? MillisecondsAveragePageLoadTime { [UsedImplicitly] get; set; }

        public int? RemainingWorkload { [UsedImplicitly] get; set; }

        public bool? RestrictHumanInteraction { [UsedImplicitly] get; set; }

        public string StatusText { [UsedImplicitly] get; set; }

        public int? ValidUrlCount { [UsedImplicitly] get; set; }

        public int? VerifiedUrlCount { [UsedImplicitly] get; set; }
    }
}