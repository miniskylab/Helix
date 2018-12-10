namespace Helix.Crawler.Abstractions
{
    public interface IVerificationResult
    {
        int HttpStatusCode { get; }

        bool IsBrokenResource { get; }

        bool IsInternalResource { get; }

        IRawResource RawResource { get; }

        IResource Resource { get; }
    }
}