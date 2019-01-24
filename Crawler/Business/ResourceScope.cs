using System;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class ResourceScope : IResourceScope
    {
        readonly Configurations _configurations;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ResourceScope(Configurations configurations) { _configurations = configurations; }

        public bool IsInternalResource(Resource resource)
        {
            if (resource == null || resource.Uri == null) throw new ArgumentNullException();
            if (IsStartUri(resource.Uri)) return true;
            if (resource.Uri.Authority.Equals(_configurations.DomainName, StringComparison.OrdinalIgnoreCase)) return true;

            if (resource.ParentUri == null) throw new ArgumentException();
            return resource.Uri.Authority.Equals(resource.ParentUri.Authority, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsStartUri(Uri uri)
        {
            if (uri == null) throw new ArgumentNullException();
            return uri.Equals(_configurations.StartUri);
        }

        public Uri Localize(Uri uri)
        {
            if (uri == null) throw new ArgumentNullException();
            if (!uri.Authority.Equals(_configurations.DomainName, StringComparison.OrdinalIgnoreCase)) return uri;

            var uriBuilder = new UriBuilder(uri) { Host = _configurations.StartUri.Host, Port = _configurations.StartUri.Port };
            return uriBuilder.Uri;
        }
    }
}