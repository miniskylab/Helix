using System.Threading.Tasks.Dataflow;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceEnricherBlock : IPropagatorBlock<Resource, Resource>
    {
        BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }
    }
}