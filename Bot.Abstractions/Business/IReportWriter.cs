using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IReportWriter : IService
    {
        void WriteReport(VerificationResult verificationResult);
    }
}