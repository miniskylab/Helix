namespace Helix.Crawler.Abstractions
{
    public interface IRawResource : INetworkResource
    {
        string ParentUrl { get; set; }

        string Url { get; set; }
    }
}