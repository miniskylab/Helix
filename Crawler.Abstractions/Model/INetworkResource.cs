namespace Helix.Crawler.Abstractions
{
    public interface INetworkResource
    {
        int HttpStatusCode { get; }
    }
}