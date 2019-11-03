using System;

namespace Helix.Bot.Abstractions
{
    public interface IResourceScope
    {
        bool IsInternalResource(Resource resource);

        bool IsStartUri(Uri uri);

        Uri Localize(Uri uri);
    }
}