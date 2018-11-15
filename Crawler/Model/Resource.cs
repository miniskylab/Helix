using System;
using Helix.Abstractions;

namespace Helix.Implementations
{
    public class Resource : IResource
    {
        public Uri ParentUri { get; set; }

        public bool Transformed { get; set; }

        public Uri Uri { get; set; }
    }
}