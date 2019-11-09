using System;
using System.Threading.Tasks.Dataflow;

namespace Helix.Bot.Abstractions
{
    public interface IHtmlRendererBlock : IPropagatorBlock<Tuple<IHtmlRenderer, Resource>, RenderingResult>,
                                          IReceivableSourceBlock<RenderingResult>
    {
        BufferBlock<Event> Events { get; }

        BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        BufferBlock<IHtmlRenderer> HtmlRenderers { get; }

        int InputCount { get; }

        int OutputCount { get; }

        BufferBlock<VerificationResult> VerificationResults { get; }
    }
}