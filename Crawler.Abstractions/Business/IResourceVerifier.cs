using System;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceVerifier : IDisposable
    {
        event IdleEvent OnIdle;

        bool TryVerify(RawResource rawResource, out VerificationResult verificationResult);
    }
}