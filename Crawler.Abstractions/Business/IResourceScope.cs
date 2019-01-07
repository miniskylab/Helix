using System;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceScope
    {
        bool IsInternalResource(Resource resource);

        bool IsStartUrl(string url);

        Uri Localize(Uri uri);
    }
}