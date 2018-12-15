using System;

namespace Helix.Crawler.Abstractions
{
    public interface IResource : INetworkResource
    {
        bool Localized { get; set; }

        Uri ParentUri { get; set; }

        Uri Uri { get; set; }
    }
}