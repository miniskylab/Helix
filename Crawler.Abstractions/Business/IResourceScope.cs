using System;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceScope
    {
        bool IsInternalResource(Resource resource);

        bool IsStartUri(string url);

        Uri Localize(Uri uri);
    }
}