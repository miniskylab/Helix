namespace Helix.Abstractions
{
    public interface IVerificationResult
    {
        bool IsBrokenResource { get; }

        bool IsInternalResource { get; set; }

        IRawResource RawResource { get; set; }

        IResource Resource { get; set; }

        int StatusCode { get; set; }
    }
}