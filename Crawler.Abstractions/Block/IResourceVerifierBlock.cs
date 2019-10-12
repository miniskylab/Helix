using System.Threading.Tasks.Dataflow;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceVerifierBlock : IPropagatorBlock<Resource, Resource>
    {
        BufferBlock<Event> Events { get; }

        BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        BufferBlock<VerificationResult> VerificationResults { get; }
    }
}