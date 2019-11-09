using System.Threading.Tasks.Dataflow;

namespace Helix.Bot.Abstractions
{
    public interface IResourceVerifierBlock : IPropagatorBlock<Resource, Resource>, IReceivableSourceBlock<Resource>
    {
        BufferBlock<Resource> BrokenResources { get; }

        BufferBlock<Event> Events { get; }

        BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        int InputCount { get; }

        int OutputCount { get; }

        BufferBlock<VerificationResult> VerificationResults { get; }
    }
}