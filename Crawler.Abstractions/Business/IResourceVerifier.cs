using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceVerifier : IDisposable
    {
        bool TryVerify(Resource resource, CancellationToken cancellationToken, out VerificationResult verificationResult);
    }
}