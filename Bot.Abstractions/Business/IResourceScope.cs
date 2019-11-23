using System;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IResourceScope : IService
    {
        bool IsInternalResource(Resource resource);

        bool IsStartUri(Uri uri);

        Uri Localize(Uri uri);
    }
}