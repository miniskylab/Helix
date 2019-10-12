using System.Threading.Tasks.Dataflow;

namespace Helix.Crawler.Abstractions
{
    public interface IReportWriterBlock : ITargetBlock<VerificationResult> { }
}