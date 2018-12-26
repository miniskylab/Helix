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

            if (resource.ParentUri == null) throw new ArgumentException();
            return resource.Uri.Authority.ToLower().Equals(resource.ParentUri.Authority.ToLower()) ||
                   resource.Uri.Authority.ToLower().EndsWith(_configurations.DomainName.ToLower());
        }

        public bool IsStartUrl(string url)
        {
            if (url == null) throw new ArgumentNullException();
            var localizedUrl = new Uri(url).AbsoluteUri;
            return localizedUrl.ToLower().EnsureEndsWith('/').Equals(_configurations.StartUrl.EnsureEndsWith('/'));
        }

        public Uri Localize(Uri uri)
        {
            /*TODO:*/
            return uri;
        }
    }
}