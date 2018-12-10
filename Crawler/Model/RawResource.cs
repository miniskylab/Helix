using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class RawResource : IRawResource
    {
        public int HttpStatusCode { get; set; }

        public string ParentUrl { get; set; }

        public string Url { get; set; }
    }
}