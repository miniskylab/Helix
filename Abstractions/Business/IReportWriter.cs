using System;

namespace Helix.Abstractions
{
    public interface IReportWriter : IDisposable
    {
        void WriteReport(IVerificationResult verificationResult, bool writeBrokenLinksOnly = false);
    }
}