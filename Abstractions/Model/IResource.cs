using System;

namespace Helix.Abstractions
{
    public interface IResource
    {
        Uri ParentUri { get; set; }

        Uri Uri { get; set; }
    }
}