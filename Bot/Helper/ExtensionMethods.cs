using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Helix.Bot.Abstractions;
using Newtonsoft.Json;

namespace Helix.Bot
{
    public static class ExtensionMethods
    {
        public static string GetAbsoluteUrl(this Resource resource)
        {
            return resource.Uri != null
                ? resource.OriginalUrl.EndsWith("/") ? resource.Uri.AbsoluteUri : resource.Uri.AbsoluteUri.TrimEnd('/')
                : resource.OriginalUrl;
        }

        public static bool IsCompilerGenerated(this Type type) => type.GetCustomAttribute(typeof(CompilerGeneratedAttribute), true) != null;

        public static bool IsWithinBrokenRange(this StatusCode statusCode) { return Math.Abs((int) statusCode) >= 400; }

        public static string ToJson(this object @object) { return JsonConvert.SerializeObject(@object); }

        public static VerificationResult ToVerificationResult(this Resource resource)
        {
            return new VerificationResult
            {
                Id = resource.Id,
                IsInternalResource = resource.IsInternal,
                ParentUrl = resource.ParentUri?.AbsoluteUri,
                StatusCode = resource.StatusCode,
                VerifiedUrl = resource.GetAbsoluteUrl(),
                ResourceType = Enum.GetName(typeof(ResourceType), resource.ResourceType)
            };
        }
    }
}