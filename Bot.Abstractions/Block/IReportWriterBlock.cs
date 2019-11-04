using System.Threading.Tasks.Dataflow;

namespace Helix.Bot.Abstractions
{
    public interface IReportWriterBlock : ITargetBlock<VerificationResult>
    {
        int InputCount { get; }
    }
}