using Helix.Abstractions;

namespace Helix.Crawler
{
    public class VerificationResult : IVerificationResult
    {
        public bool IsInternalResource { get; set; }

        public IRawResource RawResource { get; set; }

        public IResource Resource { get; set; }

        public int StatusCode { get; set; }

        public bool IsBrokenResource => StatusCode < 0 || 400 <= StatusCode;
    }
}