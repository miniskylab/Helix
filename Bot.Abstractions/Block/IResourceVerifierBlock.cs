using System.Threading.Tasks.Dataflow;

namespace Helix.Bot.Abstractions
{
    public interface IResourceVerifierBlock : IPropagatorBlock<Resource, Resource>, IReceivableSourceBlock<Resource>
    {
        BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        BufferBlock<VerificationResult> VerificationResults { get; }
    }
}