using System;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class RawResourceProcessor : IRawResourceProcessor
    {
        readonly IResourceScope _resourceScope;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public RawResourceProcessor(IResourceScope resourceScope) { _resourceScope = resourceScope; }

        public bool TryProcessRawResource(RawResource rawResource, out Resource resource)
        {
            if (rawResource == null) throw new ArgumentNullException();

            resource = null;
            if (!TryCreateAbsoluteUri(out var uri)) return false;
            if (!UriSchemeIsSupported()) return false;
            if (IsOrphanedUri()) return false;
            StripFragment();

            resource = new Resource { ParentUri = rawResource.ParentUri, Uri = uri, HttpStatusCode = rawResource.HttpStatusCode };
            return true;

            bool TryCreateAbsoluteUri(out Uri absoluteUri)
            {
                absoluteUri = null;
                if (!Uri.TryCreate(rawResource.Url, UriKind.RelativeOrAbsolute, out var relativeOrAbsoluteUri)) return false;
                if (relativeOrAbsoluteUri.IsAbsoluteUri)
                {
                    absoluteUri = relativeOrAbsoluteUri;
                    return true;
                }

                absoluteUri = new Uri(rawResource.ParentUri, rawResource.Url);
                return true;
            }
            bool UriSchemeIsSupported() { return uri.Scheme == "http" || uri.Scheme == "https"; }
            bool IsOrphanedUri()
            {
                // TODO: Investigate where those orphaned Uri-s came from.
                return rawResource.ParentUri == null && !_resourceScope.IsStartUri(uri);
            }
            void StripFragment()
            {
                if (string.IsNullOrWhiteSpace(uri.Fragment)) return;
                uri = new Uri(uri.AbsoluteUri.Replace(uri.Fragment, string.Empty));
            }
        }
    }
}