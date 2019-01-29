using System;

namespace Helix.Crawler.Abstractions
{
    public interface IRawResourceVerifier : IDisposable
    {
        event Action OnIdle;

        bool TryVerify(RawResource rawResource, out VerificationResult verificationResult);
    }
}