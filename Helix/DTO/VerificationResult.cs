using System;

namespace Helix
{
    public class VerificationResult
    {
        public bool IsInternalUrl { get; set; }
        public int StatusCode { get; set; }
        public Uri Uri { get; set; }
    }
}