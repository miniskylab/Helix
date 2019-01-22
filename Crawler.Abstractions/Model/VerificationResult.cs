namespace Helix.Crawler.Abstractions
{
    public class VerificationResult
    {
        public int HttpStatusCode { get; set; }

        public bool IsInternalResource { get; set; }

        public RawResource RawResource { get; set; }

        public Resource Resource { get; set; }

        public bool IsBrokenResource => HttpStatusCode < 0 || 400 <= HttpStatusCode;

        public bool IsExtractedResource => Resource != null && RawResource?.HttpStatusCode == 0;
    }
}