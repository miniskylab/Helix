using System;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class RawResourceProcessor : IRawResourceProcessor
    {
        readonly IResourceScope _resourceScope;

        public RawResourceProcessor(IResourceScope resourceScope) { _resourceScope = resourceScope; }

        public bool TryProcessRawResource(RawResource rawResource, out Resource resource)
        {
            if (rawResource == null) throw new ArgumentNullException();

            resource = null;
            var absoluteUrl = EnsureAbsolute(rawResource.Url, rawResource.ParentUri);
            if (!_resourceScope.IsStartUrl(absoluteUrl) && rawResource.ParentUri == null) return false;

            if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri)) return false;
            StripFragmentFrom(ref uri);

            resource = new Resource { ParentUri = rawResource.ParentUri, Uri = uri, HttpStatusCode = rawResource.HttpStatusCode };
            return true;
        }

        static string EnsureAbsolute(string possiblyRelativeUrl, Uri parentUri)
        {
            if (!possiblyRelativeUrl.StartsWith("/")) return possiblyRelativeUrl;
            if (parentUri == null) throw new ArgumentException();
            var baseString = possiblyRelativeUrl.StartsWith("//")
                ? $"{parentUri.Scheme}:"
                : $"{parentUri.Scheme}://{parentUri.Host}:{parentUri.Port}";
            return $"{baseString}{possiblyRelativeUrl}";
        }

        static void StripFragmentFrom(ref Uri uri)
        {
            if (string.IsNullOrWhiteSpace(uri.Fragment)) return;
            uri = new Uri(uri.AbsoluteUri.Replace(uri.Fragment, string.Empty));
        }
    }
}