using System.Threading.Tasks.Dataflow;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IProcessingResultGeneratorBlock : IPropagatorBlock<RenderingResult, ProcessingResult>, IService,
                                                       IReceivableSourceBlock<ProcessingResult>
    {
        int InputCount { get; }

        int OutputCount { get; }
    }
}