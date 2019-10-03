using System;

namespace Helix.Crawler.Abstractions
{
    public class HtmlDocument
    {
        public string HtmlText { get; set; }

        public Uri Uri { get; set; }
    }
}