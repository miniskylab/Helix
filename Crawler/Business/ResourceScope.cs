using System;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class ResourceScope : IResourceScope
    {
        readonly Configurations _configurations;

        public ResourceScope(Configurations configurations) { _configurations = configurations; }

        public bool IsInternalResource(IResource resource)
        {
            if (resource == null || resource.Uri == null) throw new ArgumentNullException();
            if (IsStartUrl(resource.Uri.AbsoluteUri)) return true;
            if (resource.Uri.Authority.Equals(_configurations.DomainName, StringComparison.OrdinalIgnoreCase)) return true;

            if (resource.ParentUri == null) throw new ArgumentException();
            return resource.Uri.Authority.Equals(resource.ParentUri.Authority, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsStartUrl(string url)
        {
            if (url == null) throw new ArgumentNullException();
            return new Uri(url).AbsoluteUri.Equals(_configurations.StartUrl.EnsureEndsWith('/'), StringComparison.OrdinalIgnoreCase);
        }

        public Uri Localize(Uri uri)
        {
            if (uri == null) throw new ArgumentNullException();
            if (!uri.Authority.Equals(_configurations.DomainName, StringComparison.OrdinalIgnoreCase)) return uri;

            var startUri = new Uri(_configurations.StartUrl);
            var uriBuilder = new UriBuilder(uri) { Host = startUri.Host, Port = startUri.Port };
            return uriBuilder.Uri;
        }
    }
}