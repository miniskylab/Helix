using System.Threading.Tasks.Dataflow;

namespace Helix.Bot.Abstractions
{
    public interface IHtmlRendererBlock : IPropagatorBlock<Resource, RenderingResult>, IReceivableSourceBlock<RenderingResult>
    {
        BufferBlock<Event> Events { get; }

        BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        int InputCount { get; }

        int OutputCount { get; }

        BufferBlock<VerificationResult> VerificationResults { get; }
    }
}