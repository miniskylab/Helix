using System;
using System.Threading.Tasks.Dataflow;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IHtmlRendererBlock : IPropagatorBlock<Tuple<IHtmlRenderer, Resource>, RenderingResult>, IService,
                                          IReceivableSourceBlock<RenderingResult>
    {
        BufferBlock<Event> Events { get; }

        BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        BufferBlock<IHtmlRenderer> HtmlRenderers { get; }

        int InputCount { get; }

        int OutputCount { get; }

        BufferBlock<(ReportWritingAction, VerificationResult)> ReportWritingMessages { get; }
    }
}