namespace Helix.Crawler.Abstractions
{
    public interface IRawResource : INetworkResource
    {
        string ParentUrl { get; }

        string Url { get; }
    }
}