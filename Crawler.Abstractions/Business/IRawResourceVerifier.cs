using System;

namespace Helix.Crawler.Abstractions
{
    public interface IRawResourceVerifier : IDisposable
    {
        bool TryVerify(RawResource rawResource, out VerificationResult verificationResult);
    }
}