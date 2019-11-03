using System;

namespace Helix.Bot.Abstractions
{
    public interface IReportWriter : IDisposable
    {
        void WriteReport(VerificationResult verificationResult);
    }
}