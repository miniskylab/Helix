using System;
using System.Data;
using Helix.Bot.Abstractions;

namespace Helix.Bot
{
    public sealed class ResourceEnricher : IResourceEnricher
    {
        readonly IIncrementalIdGenerator _incrementalIdGenerator;
        readonly IResourceScope _resourceScope;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ResourceEnricher(IResourceScope resourceScope, IIncrementalIdGenerator incrementalIdGenerator)
        {
            _resourceScope = resourceScope;
            _incrementalIdGenerator = incrementalIdGenerator;
        }

        public Resource Enrich(Resource resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (resource.StatusCode != default && resource.Uri == null) throw new InvalidConstraintException();
            if (resource.StatusCode == default && resource.Uri != null) throw new InvalidConstraintException();
            if (resource.Id != 0) return resource;

            resource.Id = _incrementalIdGenerator.GetNext();
            if (resource.StatusCode == default)
            {
                if (!TryCreateAbsoluteUri()) resource.StatusCode = StatusCode.MalformedUri;
                else if (UriSchemeIsNotSupported()) resource.StatusCode = StatusCode.UriSchemeNotSupported;
                else if (IsOrphanedUri()) resource.StatusCode = StatusCode.OrphanedUri;
                else StripFragment();

                resource.Uri = resource.OriginalUri;
            }

            if (resource.Uri != null) resource.IsInternal = _resourceScope.IsInternalResource(resource);
            return resource;

            bool TryCreateAbsoluteUri()
            {
                if (!Uri.TryCreate(resource.OriginalUrl, UriKind.RelativeOrAbsolute, out var relativeOrAbsoluteUri)) return false;
                if (relativeOrAbsoluteUri.IsAbsoluteUri)
                {
                    resource.OriginalUri = relativeOrAbsoluteUri;
                    return true;
                }

                if (!Uri.TryCreate(resource.ParentUri, resource.OriginalUrl, out var absoluteUri)) return false;
                resource.OriginalUri = absoluteUri;
                return true;
            }
            bool UriSchemeIsNotSupported() { return resource.OriginalUri.Scheme != "http" && resource.OriginalUri.Scheme != "https"; }
            bool IsOrphanedUri()
            {
                // TODO: Investigate where those orphaned Uri-s came from.
                return resource.ParentUri == null && !_resourceScope.IsStartUri(resource.OriginalUri);
            }
            void StripFragment()
            {
                if (string.IsNullOrWhiteSpace(resource.OriginalUri.Fragment)) return;
                resource.OriginalUri = new UriBuilder(resource.OriginalUri) { Fragment = string.Empty }.Uri;
            }
        }
    }
}