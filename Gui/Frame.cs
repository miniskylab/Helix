using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Helix.Gui
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class Frame
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public BorderColor? BorderColor { get; set; }

        public int? BrokenUrlCount { get; set; }

        public bool? DisableCloseButton { get; set; }

        public bool? DisableConfigurationPanel { get; set; }

        public bool? DisableMainButton { get; set; }

        public bool? DisablePreviewButton { get; set; }

        public bool? DisableStopButton { get; set; }

        public string ElapsedTime { get; set; }

        public double? MillisecondsAveragePageLoadTime { get; set; }

        public int? RemainingWorkload { get; set; }

        public bool? ShowWaitingOverlay { get; set; }

        public string StatusText { get; set; }

        public int? ValidUrlCount { get; set; }

        public int? VerifiedUrlCount { get; set; }

        public string WaitingOverlayProgressText { get; set; }
    }

    public enum BorderColor
    {
        Normal,
        Error
    }
}