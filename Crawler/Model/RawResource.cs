using Helix.Abstractions;

namespace Helix.Implementations
{
    public class RawResource : IRawResource
    {
        public string ParentUrl { get; set; }

        public string Url { get; set; }
    }
}