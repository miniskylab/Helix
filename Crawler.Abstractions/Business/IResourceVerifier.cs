using System.Threading;
using System.Threading.Tasks;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceVerifier
    {
        Task<VerificationResult> Verify(Resource resource, CancellationToken cancellationToken);
    }
}