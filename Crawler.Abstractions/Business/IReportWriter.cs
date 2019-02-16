namespace Helix.Crawler.Abstractions
{
    public interface IReportWriter
    {
        void WriteReport(VerificationResult verificationResult);
    }
}