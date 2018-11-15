using System;
using Helix.Abstractions;

namespace Helix.Implementations
{
    public class ResourceScope : IResourceScope
    {
        readonly IConfigurations _configurations;

        public ResourceScope(IConfigurations configurations) { _configurations = configurations; }

        public void EnsureInternal(Uri uri)
        {
            /*TODO:*/
        }

        public bool IsInternalResource(IResource resource)
        {
            return IsStartUrl(resource.Uri.AbsoluteUri) ||
                   resource.Uri.Authority.ToLower().Equals(resource.ParentUri.Authority.ToLower()) ||
                   resource.Uri.Authority.ToLower().EndsWith(_configurations.DomainName.ToLower());
        }

        public bool IsStartUrl(string url)
        {
            return url.ToLower().EnsureEndsWith('/').Equals(_configurations.StartUrl.EnsureEndsWith('/'));
        }
    }
}