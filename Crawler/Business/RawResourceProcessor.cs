using System;
using Helix.Abstractions;

namespace Helix.Crawler
{
    class RawResourceProcessor : IRawResourceProcessor
    {
        readonly IResourceScopeIdentifier _resourceScopeIdentifier;

        public RawResourceProcessor(IResourceScopeIdentifier resourceScopeIdentifier)
        {
            _resourceScopeIdentifier = resourceScopeIdentifier;
        }

        public bool TryProcessRawResource(IRawResource rawResource, out IResource resource)
        {
            resource = null;
            var urlIsNotStartUrl = !_resourceScopeIdentifier.IsStartUrl(rawResource.Url);
            var parentUrlIsNotValid = !Uri.TryCreate(rawResource.ParentUrl, UriKind.Absolute, out var parentUri);
            if (urlIsNotStartUrl && parentUrlIsNotValid) return false;

            var absoluteUrl = EnsureAbsolute(rawResource.Url, parentUri);
            if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri)) return false;
            StripFragmentFrom(ref uri);

            resource = new Resource { Uri = uri, ParentUri = parentUri };
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