using System.Threading.Tasks.Dataflow;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IReportWriterBlock : ITargetBlock<(ReportWritingAction, VerificationResult)>, IService
    {
        int InputCount { get; }
    }
}