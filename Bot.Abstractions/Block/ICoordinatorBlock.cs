using System.Threading.Tasks.Dataflow;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface ICoordinatorBlock : IPropagatorBlock<ProcessingResult, Resource>, IReceivableSourceBlock<Resource>, IService
    {
        BufferBlock<Event> Events { get; }

        int InputCount { get; }

        int OutputCount { get; }

        BufferBlock<VerificationResult> VerificationResults { get; }

        bool TryActivateWorkflow(string startUrl);
    }
}