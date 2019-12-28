using System;
using System.Collections.Generic;

namespace Helix.Bot.Abstractions
{
    public class HttpContentTypeToResourceTypeDictionary : IHttpContentTypeToResourceTypeDictionary
    {
        readonly List<KeyValuePair<string, ResourceType>> _httpContentTypeToResourceTypeMapping;

        public HttpContentTypeToResourceTypeDictionary()
        {
            _httpContentTypeToResourceTypeMapping = new List<KeyValuePair<string, ResourceType>>
            {
                _("text/html", ResourceType.Html),
                _("text/css", ResourceType.Css),

                _("application/javascript", ResourceType.Script),
                _("application/x-javascript", ResourceType.Script),
                _("application/ecmascript", ResourceType.Script),
                _("text/javascript", ResourceType.Script),

                _("application/json", ResourceType.Json),
                _("application/xml", ResourceType.Xml),
                _("text/event-stream", ResourceType.ServerSentEvent),

                _("image/", ResourceType.Image),
                _("audio/", ResourceType.Audio),
                _("video/", ResourceType.Video),

                _("font/", ResourceType.Font),
                _("application/font", ResourceType.Font),

                _("text/", ResourceType.Text),
                _("application/", ResourceType.Blob)
            };

            static KeyValuePair<string, ResourceType> _(string httpContentTypePrefix, ResourceType resourceType)
            {
                return new KeyValuePair<string, ResourceType>(httpContentTypePrefix, resourceType);
            }
        }

        public ResourceType this[string httpContentType]
        {
            get
            {
                if (httpContentType == null) return ResourceType.Unknown;
                foreach (var (httpContentTypePrefix, resourceType) in _httpContentTypeToResourceTypeMapping)
                    if (httpContentType.StartsWith(httpContentTypePrefix, StringComparison.OrdinalIgnoreCase))
                        return resourceType;
                return ResourceType.Unknown;
            }
        }
    }
}