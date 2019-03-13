using System;
using System.Data;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class ResourceProcessor : IResourceProcessor
    {
        readonly IIncrementalIdGenerator _incrementalIdGenerator;
        readonly IResourceScope _resourceScope;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ResourceProcessor(IResourceScope resourceScope, IIncrementalIdGenerator incrementalIdGenerator)
        {
            _resourceScope = resourceScope;
            _incrementalIdGenerator = incrementalIdGenerator;
        }

        public Resource Categorize(Resource resource, string contentType)
        {
            if (contentType == null) return resource;
            const StringComparison ordinalIgnoreCase = StringComparison.OrdinalIgnoreCase;
            if (contentType.StartsWith("text/css", ordinalIgnoreCase)) resource.ResourceType = ResourceType.Css;
            if (contentType.StartsWith("text/html", ordinalIgnoreCase)) resource.ResourceType = ResourceType.Html;
            else if (contentType.StartsWith("image/", ordinalIgnoreCase)) resource.ResourceType = ResourceType.Image;
            else if (contentType.StartsWith("audio/", ordinalIgnoreCase)) resource.ResourceType = ResourceType.Audio;
            else if (contentType.StartsWith("video/", ordinalIgnoreCase)) resource.ResourceType = ResourceType.Video;
            else if (contentType.StartsWith("font/", ordinalIgnoreCase)) resource.ResourceType = ResourceType.Font;
            else if (contentType.StartsWith("application/javascript", ordinalIgnoreCase)) resource.ResourceType = ResourceType.Script;
            else if (contentType.StartsWith("application/ecmascript", ordinalIgnoreCase)) resource.ResourceType = ResourceType.Script;
            else if (contentType.StartsWith("application/octet-stream", ordinalIgnoreCase)) resource.ResourceType = ResourceType.Blob;
            return resource;
        }

        public Resource Enrich(Resource resource)
        {
            if (resource == null) throw new ArgumentNullException();
            if (resource.StatusCode != default && resource.Uri == null) throw new InvalidConstraintException();

            resource.Id = _incrementalIdGenerator.GetNext();
            if (resource.StatusCode != default) return resource;
            if (!TryCreateAbsoluteUri()) resource.StatusCode = StatusCode.MalformedUri;
            else if (UriSchemeIsNotSupported()) resource.StatusCode = StatusCode.UriSchemeNotSupported;
            else if (IsOrphanedUri()) resource.StatusCode = StatusCode.OrphanedUri;
            else
            {
                StripFragment();
                resource.IsInternal = _resourceScope.IsInternalResource(resource);
            }
            return resource;

            bool TryCreateAbsoluteUri()
            {
                if (!Uri.TryCreate(resource.OriginalUrl, UriKind.RelativeOrAbsolute, out var relativeOrAbsoluteUri)) return false;
                if (relativeOrAbsoluteUri.IsAbsoluteUri)
                {
                    resource.Uri = relativeOrAbsoluteUri;
                    return true;
                }

                resource.Uri = new Uri(resource.ParentUri, resource.OriginalUrl);
                return true;
            }
            bool UriSchemeIsNotSupported() { return resource.Uri.Scheme != "http" && resource.Uri.Scheme != "https"; }
            bool IsOrphanedUri()
            {
                // TODO: Investigate where those orphaned Uri-s came from.
                return resource.ParentUri == null && !_resourceScope.IsStartUri(resource.Uri);
            }
            void StripFragment()
            {
                if (string.IsNullOrWhiteSpace(resource.Uri.Fragment)) return;
                resource.Uri = new UriBuilder(resource.Uri) { Fragment = string.Empty }.Uri;
            }
        }
    }
}