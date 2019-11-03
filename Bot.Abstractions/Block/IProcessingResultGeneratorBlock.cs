using System.Threading.Tasks.Dataflow;

namespace Helix.Bot.Abstractions
{
    public interface IProcessingResultGeneratorBlock :
        IPropagatorBlock<RenderingResult, ProcessingResult>,
        IReceivableSourceBlock<ProcessingResult> { }
}