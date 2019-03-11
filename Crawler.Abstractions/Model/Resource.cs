using System;

namespace Helix.Crawler.Abstractions
{
    public class Resource
    {
        public int Id { get; set; }

        public bool IsExtracted { get; set; }

        public bool IsInternal { get; set; }

        // public bool Localized { get; set; }

        public string OriginalUrl { get; set; }

        public Uri ParentUri { get; set; } // TODO: Potential redundant trailing slash

        public ResourceType ResourceType { get; set; }

        public long? Size { get; set; }

        public StatusCode StatusCode { get; set; }

        public Uri Uri { get; set; }

        public string AbsoluteUrl => OriginalUrl.EndsWith("/") ? Uri?.AbsoluteUri : Uri?.AbsoluteUri.TrimEnd('/');

        public bool IsBroken => StatusCode < 0 || 400 <= (int) StatusCode;
    }
}