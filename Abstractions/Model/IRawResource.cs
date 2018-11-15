namespace Helix.Abstractions
{
    public interface IRawResource
    {
        string ParentUrl { get; set; }

        string Url { get; set; }
    }
}