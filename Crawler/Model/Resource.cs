using System;
using Helix.Abstractions;

namespace Helix.Implementations
{
    public class Resource : IResource
    {
        public int HttpStatusCode { get; set; }

        public bool Localized { get; set; }

        public Uri ParentUri { get; set; }

        public Uri Uri { get; set; }
    }
}