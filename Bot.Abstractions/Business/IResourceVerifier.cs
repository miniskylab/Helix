using System.Threading;
using System.Threading.Tasks;

namespace Helix.Bot.Abstractions
{
    public interface IResourceVerifier
    {
        Task<VerificationResult> Verify(Resource resource, CancellationToken cancellationToken);
    }
}