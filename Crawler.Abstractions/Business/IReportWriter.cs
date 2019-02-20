using System;

namespace Helix.Crawler.Abstractions
{
    public interface IReportWriter : IDisposable
    {
        void UpdateStatusCode(int resourceId, HttpStatusCode newStatusCode);

        void WriteReport(VerificationResult verificationResult);
    }
}