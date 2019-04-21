using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Helix.Gui
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class Frame
    {
        public int? BrokenUrlCount { [UsedImplicitly] get; set; }

        public bool? DisableMainButton { [UsedImplicitly] get; set; }

        public string ElapsedTime { [UsedImplicitly] get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public MainButtonFunctionality? MainButtonFunctionality { [UsedImplicitly] get; set; }

        public double? MillisecondsAveragePageLoadTime { [UsedImplicitly] get; set; }

        public int? RemainingWorkload { [UsedImplicitly] get; set; }

        public string StatusText { [UsedImplicitly] get; set; }

        public int? ValidUrlCount { [UsedImplicitly] get; set; }

        public int? VerifiedUrlCount { [UsedImplicitly] get; set; }
    }

    public enum MainButtonFunctionality
    {
        Start,
        Pause
    }
}