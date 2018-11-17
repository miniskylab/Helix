namespace Helix.Abstractions
{
    public interface IVerificationResult
    {
        int HttpStatusCode { get; set; }

        bool IsBrokenResource { get; }

        bool IsInternalResource { get; set; }

        IRawResource RawResource { get; set; }

        IResource Resource { get; set; }
    }
}