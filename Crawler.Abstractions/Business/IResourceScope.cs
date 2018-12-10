using System;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceScope
    {
        void EnsureInternal(Uri uri);

        bool IsInternalResource(IResource resource);

        bool IsStartUrl(string url);
    }
}