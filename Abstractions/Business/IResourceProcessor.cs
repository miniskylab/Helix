namespace Helix.Abstractions
{
    public interface IResourceProcessor
    {
        bool TryProcessRawResource(IRawResource rawResource, out IResource resource);
    }
}