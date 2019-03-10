using System;

namespace Helix.Crawler.Abstractions
{
    public interface IReportWriter : IDisposable
    {
        void UpdateStatusCode(int resourceId, StatusCode newStatusCode);

        void WriteReport(VerificationResult verificationResult);
    }
}