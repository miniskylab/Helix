using System;
using System.Collections.Generic;

namespace Helix.Crawler.Abstractions
{
    public class ContentTypeToResourceTypeDictionary : IContentTypeToResourceTypeDictionary
    {
        readonly Dictionary<string, ResourceType> _contentTypeToResourceTypeDictionary = new Dictionary<string, ResourceType>();

        public ContentTypeToResourceTypeDictionary()
        {
            _contentTypeToResourceTypeDictionary["text/css"] = ResourceType.Css;
            _contentTypeToResourceTypeDictionary["text/html"] = ResourceType.Html;
            _contentTypeToResourceTypeDictionary["application/pdf"] = ResourceType.Pdf;
            _contentTypeToResourceTypeDictionary["application/javascript"] = ResourceType.Script;
            _contentTypeToResourceTypeDictionary["application/ecmascript"] = ResourceType.Script;
            _contentTypeToResourceTypeDictionary["application/octet-stream"] = ResourceType.Blob;
        }

        public ResourceType this[string contentType]
        {
            get
            {
                if (_contentTypeToResourceTypeDictionary.TryGetValue(contentType.ToLower(), out var resourceType)) return resourceType;
                if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return ResourceType.Image;
                if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return ResourceType.Audio;
                if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return ResourceType.Video;
                return contentType.StartsWith("font/", StringComparison.OrdinalIgnoreCase) ? ResourceType.Font : ResourceType.Unknown;
            }
        }
    }
}