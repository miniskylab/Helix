using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceVerifier : IDisposable
    {
        VerificationResult Verify(Resource resource, CancellationToken cancellationToken);
    }
}