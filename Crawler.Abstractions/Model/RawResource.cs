namespace Helix.Crawler.Abstractions
{
    public class RawResource : NetworkResource
    {
        public string ParentUrl { get; set; }

        public string Url { get; set; }
    }
}