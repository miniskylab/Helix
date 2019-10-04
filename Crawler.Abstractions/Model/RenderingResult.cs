using System.Collections.Generic;

namespace Helix.Crawler.Abstractions
{
    public class RenderingResult
    {
        public List<Resource> CapturedResources { get; set; }

        public HtmlDocument HtmlDocument { get; set; }

        public Resource RenderedResource { get; set; }
    }
}