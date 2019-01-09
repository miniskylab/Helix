using System;

namespace Helix.Crawler.Abstractions
{
    public class RawResource : NetworkResource
    {
        public Uri ParentUri { get; set; }

        public string Url { get; set; }
    }
}