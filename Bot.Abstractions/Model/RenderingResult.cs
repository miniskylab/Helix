using System.Collections.Generic;

namespace Helix.Bot.Abstractions
{
    public class RenderingResult
    {
        public List<Resource> CapturedResources { get; set; }

        public HtmlDocument HtmlDocument { get; set; }

        public long? MillisecondsPageLoadTime { get; set; }

        public Resource RenderedResource { get; set; }
    }
}