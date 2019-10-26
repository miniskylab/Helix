using System.Threading.Tasks.Dataflow;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceVerifierBlock : IPropagatorBlock<Resource, Resource>, IReceivableSourceBlock<Resource>
    {
        BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        BufferBlock<VerificationResult> VerificationResults { get; }
    }
}