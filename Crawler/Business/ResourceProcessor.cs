using System;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class ResourceProcessor : IResourceProcessor
    {
        readonly IResourceScope _resourceScope;

        public ResourceProcessor(IResourceScope resourceScope) { _resourceScope = resourceScope; }

        public bool TryProcessRawResource(RawResource rawResource, out Resource resource)
        {
            if (rawResource == null) throw new ArgumentNullException();

            resource = null;
            var urlIsNotStartUrl = !_resourceScope.IsStartUrl(rawResource.Url);
            var parentUrlIsNotValid = !Uri.TryCreate(rawResource.ParentUrl, UriKind.Absolute, out var parentUri);
            if (urlIsNotStartUrl && parentUrlIsNotValid) return false;

            var absoluteUrl = EnsureAbsolute(rawResource.Url, parentUri);
            if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri)) return false;
            StripFragmentFrom(ref uri);

            resource = new Resource { Uri = uri, ParentUri = parentUri, HttpStatusCode = rawResource.HttpStatusCode };
            return true;
        }

        static string EnsureAbsolute(string possiblyRelativeUrl, Uri parentUri)
        {
            if (parentUri == null || !possiblyRelativeUrl.StartsWith("/")) return possiblyRelativeUrl;
            var baseString = possiblyRelativeUrl.StartsWith("//")
                ? $"{parentUri.Scheme}:"
                : $"{parentUri.Scheme}://{parentUri.Host}:{parentUri.Port}";
            return $"{baseString}/{possiblyRelativeUrl}";
        }

        static void StripFragmentFrom(ref Uri uri)
        {
            if (string.IsNullOrWhiteSpace(uri.Fragment)) return;
            uri = new Uri(uri.AbsoluteUri.Replace(uri.Fragment, string.Empty));
        }
    }
}