using System.Collections.Generic;

namespace Helix.Crawler.Abstractions
{
    public class RenderingResult
    {
        public HtmlDocument HtmlDocument { get; set; }

        public List<Resource> NewResources { get; set; }
    }
}