using System;
using Helix.Abstractions;

namespace Helix.Crawler
{
    public class Resource : IResource
    {
        public Uri ParentUri { get; set; }

        public Uri Uri { get; set; }
    }
}