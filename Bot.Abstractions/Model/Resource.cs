using System;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public class Resource
    {
        public int Id { get; }

        public bool IsExtractedFromHtmlDocument { get; }

        public bool IsInternal { get; set; }

        public Uri OriginalUri { get; }

        // TODO:
        // public bool Localized { get; set; }

        public Uri ParentUri { get; }

        public ResourceType ResourceType { get; set; }

        public long? Size { get; set; }

        public StatusCode StatusCode { get; set; }

        public Uri Uri { get; set; }

        public Resource(int id, string originalUrl, Uri parentUri, bool isExtractedFromHtmlDocument)
        {
            Id = id;
            ParentUri = parentUri;
            IsExtractedFromHtmlDocument = isExtractedFromHtmlDocument;

            if (Uri.TryCreate(originalUrl, UriKind.RelativeOrAbsolute, out var relativeOrAbsoluteUri))
            {
                if (relativeOrAbsoluteUri.IsAbsoluteUri) OriginalUri = relativeOrAbsoluteUri.StripFragment();
                else if (Uri.TryCreate(parentUri, originalUrl, out var absoluteUri)) OriginalUri = absoluteUri.StripFragment();
                else StatusCode = StatusCode.MalformedUri;
            }
            else StatusCode = StatusCode.MalformedUri;

            if (StatusCode == default && UriSchemeIsNotSupported())
                StatusCode = StatusCode.UriSchemeNotSupported;

            Uri = OriginalUri;

            #region Local Functions

            bool UriSchemeIsNotSupported() { return OriginalUri.Scheme != "http" && OriginalUri.Scheme != "https"; }

            #endregion
        }
    }
}