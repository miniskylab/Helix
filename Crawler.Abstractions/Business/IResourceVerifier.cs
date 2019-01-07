using System;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceVerifier : IDisposable
    {
        event IdleEvent OnIdle;

        VerificationResult Verify(RawResource rawResource);
    }
}