using System;

namespace Helix.Crawler.Abstractions
{
    public class Resource : NetworkResource
    {
        public int Id { get; set; }

        public bool Localized { get; set; }

        public Uri ParentUri { get; set; }

        public Uri Uri { get; set; }
    }
}