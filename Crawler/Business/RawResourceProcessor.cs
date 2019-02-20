using System;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class RawResourceProcessor : IRawResourceProcessor
    {
        readonly IIncrementalIdGenerator _incrementalIdGenerator;
        readonly IResourceScope _resourceScope;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public RawResourceProcessor(IResourceScope resourceScope, IIncrementalIdGenerator incrementalIdGenerator)
        {
            _resourceScope = resourceScope;
            _incrementalIdGenerator = incrementalIdGenerator;
        }

        public void ProcessRawResource(RawResource rawResource, out Resource resource)
        {
            if (rawResource == null) throw new ArgumentNullException();

            resource = new Resource
            {
                Id = _incrementalIdGenerator.GetNext(),
                ParentUri = rawResource.ParentUri
            };
            if (!TryCreateAbsoluteUri(out var uri)) resource.HttpStatusCode = HttpStatusCode.MalformedUri;
            else if (UriSchemeIsNotSupported()) resource.HttpStatusCode = HttpStatusCode.UriSchemeNotSupported;
            else if (IsOrphanedUri()) resource.HttpStatusCode = HttpStatusCode.OrphanedUri;
            else
            {
                StripFragment();
                resource.Uri = uri;
                resource.HttpStatusCode = rawResource.HttpStatusCode;
            }

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
            bool UriSchemeIsNotSupported() { return uri.Scheme != "http" && uri.Scheme != "https"; }
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