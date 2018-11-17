using Helix.Abstractions;

namespace Helix.Implementations
{
    public class VerificationResult : IVerificationResult
    {
        public int HttpStatusCode { get; set; }

        public bool IsInternalResource { get; set; }

        public IRawResource RawResource { get; set; }

        public IResource Resource { get; set; }

        public bool IsBrokenResource => HttpStatusCode < 0 || 400 <= HttpStatusCode;
    }
}