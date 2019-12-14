using System.Threading.Tasks.Dataflow;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IResourceVerifierBlock : IPropagatorBlock<Resource, Resource>, IReceivableSourceBlock<Resource>, IService
    {
        BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        int InputCount { get; }

        int OutputCount { get; }
    }
}