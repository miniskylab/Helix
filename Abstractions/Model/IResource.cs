using System;

namespace Helix.Abstractions
{
    public interface IResource : INetworkResource
    {
        bool Localized { get; set; }

        Uri ParentUri { get; }

        Uri Uri { get; }
    }
}