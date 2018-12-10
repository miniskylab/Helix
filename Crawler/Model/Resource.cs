using System;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class Resource : IResource
    {
        public int HttpStatusCode { get; set; }

        public bool Localized { get; set; }

        public Uri ParentUri { get; set; }

        public Uri Uri { get; set; }
    }
}