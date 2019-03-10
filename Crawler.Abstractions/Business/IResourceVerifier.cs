using System;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceVerifier : IDisposable
    {
        bool TryVerify(Resource resource, out VerificationResult verificationResult);
    }
}