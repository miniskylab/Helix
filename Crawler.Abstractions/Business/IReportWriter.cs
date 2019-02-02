using System;

namespace Helix.Crawler.Abstractions
{
    public interface IReportWriter : IDisposable
    {
        void WriteReport(VerificationResult verificationResult, bool writeBrokenLinksOnly = false);
    }
}