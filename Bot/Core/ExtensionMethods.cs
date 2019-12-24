using System;
using Helix.Bot.Abstractions;
using Newtonsoft.Json;

namespace Helix.Bot
{
    public static class ExtensionMethods
    {
        public static bool IsWithinBrokenRange(this StatusCode statusCode) { return Math.Abs((int) statusCode) >= 400; }

        public static string ToJson(this object @object) { return JsonConvert.SerializeObject(@object); }

        public static VerificationResult ToVerificationResult(this Resource resource)
        {
            return new VerificationResult
            {
                Id = resource.Id,
                StatusCode = resource.StatusCode,
                IsInternalResource = resource.IsInternal,
                ParentUrl = resource.ParentUri?.AbsoluteUri,
                VerifiedUrl = resource.Uri?.AbsoluteUri ?? resource.OriginalUrl,
                StatusMessage = Enum.GetName(typeof(StatusCode), resource.StatusCode),
                ResourceType = Enum.GetName(typeof(ResourceType), resource.ResourceType)
            };
        }
    }
}