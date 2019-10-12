using System.Threading.Tasks.Dataflow;

namespace Helix.Crawler.Abstractions
{
    public interface IProcessingResultGeneratorBlock : IPropagatorBlock<RenderingResult, ProcessingResult> { }
}