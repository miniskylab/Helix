using System.Threading;
using System.Threading.Tasks;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IResourceVerifier : IService
    {
        Task<VerificationResult> Verify(Resource resource, CancellationToken cancellationToken);
    }
}