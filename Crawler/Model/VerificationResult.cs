namespace Crawler
{
    class VerificationResult
    {
        public bool IsInternalResource { get; set; }
        public RawResource RawResource { get; set; }
        public Resource Resource { get; set; }
        public int StatusCode { get; set; }

        public bool IsBrokenResource => StatusCode < 0 || 400 <= StatusCode;
    }
}