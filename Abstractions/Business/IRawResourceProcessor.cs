namespace Helix.Abstractions
{
    public interface IRawResourceProcessor
    {
        bool TryProcessRawResource(IRawResource rawResource, out IResource resource);
    }
}