using System.Threading.Tasks.Dataflow;

namespace Helix.Crawler.Abstractions
{
    public interface IHtmlRendererBlock : IPropagatorBlock<Resource, RenderingResult>
    {
        BufferBlock<Event> Events { get; }

        BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        BufferBlock<VerificationResult> VerificationResults { get; }
    }
}