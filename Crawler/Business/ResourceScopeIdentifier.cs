using Helix.Abstractions;

namespace Helix.Crawler
{
    public class ResourceScopeIdentifier : IResourceScopeIdentifier
    {
        readonly IConfigurations _configurations;

        public ResourceScopeIdentifier(IConfigurations configurations) { _configurations = configurations; }

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