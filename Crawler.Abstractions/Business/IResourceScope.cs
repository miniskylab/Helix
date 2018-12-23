using System;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceScope
    {
        Uri Localize(Uri uri);

        bool IsInternalResource(IResource resource);

        bool IsStartUrl(string url);
    }
}