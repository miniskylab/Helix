using System;

namespace Helix.Abstractions
{
    public interface IResourceVerifier : IDisposable
    {
        event IdleEvent OnIdle;

        IVerificationResult Verify(IRawResource rawResource);
    }
}