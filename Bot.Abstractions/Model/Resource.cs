using System;

namespace Helix.Bot.Abstractions
{
    public class Resource
    {
        public int Id { get; set; }

        public bool IsExtractedFromHtmlDocument { get; set; }

        public bool IsInternal { get; set; }

        // TODO:
        // public bool Localized { get; set; }

        public string OriginalUrl { get; set; }

        public Uri ParentUri { get; set; } // TODO: Potential redundant trailing slash

        public ResourceType ResourceType { get; set; }

        public long? Size { get; set; }

        public StatusCode StatusCode { get; set; }

        public Uri Uri { get; set; }
    }
}