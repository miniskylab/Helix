namespace Helix.Crawler.Abstractions
{
    public class VerificationResult
    {
        public bool IsInternalResource { get; set; }

        public RawResource RawResource { get; set; }

        public Resource Resource { get; set; }

        public bool IsBrokenResource => StatusCode < 0 || 400 <= (int) StatusCode;

        public bool IsExtractedResource => RawResource?.HttpStatusCode == 0;

        public string ParentUrl => RawResource.ParentUri?.OriginalString;

        public HttpStatusCode StatusCode => Resource.HttpStatusCode;

        public string VerifiedUrl
        {
            get
            {
                var verifiedUrl = RawResource.Url.EndsWith("/") ? Resource?.Uri?.AbsoluteUri : Resource?.Uri?.AbsoluteUri.TrimEnd('/');
                return verifiedUrl ?? RawResource.Url;
            }
        }
    }
}