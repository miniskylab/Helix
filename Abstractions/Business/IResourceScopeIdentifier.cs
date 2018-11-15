namespace Helix.Abstractions
{
    public interface IResourceScopeIdentifier
    {
        bool IsInternalResource(IResource resource);

        bool IsStartUrl(string url);
    }
}