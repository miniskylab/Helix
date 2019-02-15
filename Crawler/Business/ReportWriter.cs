using System;
using System.IO;
using System.Reflection;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Crawler
{
    public sealed class ReportWriter : IReportWriter
    {
        readonly Configurations _configurations;
        readonly IFilePersistence _filePersistence;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ReportWriter(Configurations configurations, IPersistenceProvider persistenceProvider)
        {
            var reportFilePath = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Report.csv";
            _filePersistence = persistenceProvider.GetFilePersistence(reportFilePath);
            _filePersistence.WriteLineAsync("Status Code,Parent Url,Verified Url");
            _configurations = configurations;
        }

        public void Dispose() { _filePersistence?.Dispose(); }

        public void WriteReport(VerificationResult verificationResult)
        {
            if (_configurations.ReportBrokenLinksOnly && !verificationResult.IsBrokenResource) return;
            var parentUrl = verificationResult.ParentUrl;
            var verifiedUrl = verificationResult.VerifiedUrl;
            _filePersistence.WriteLineAsync($"{verificationResult.StatusCode:D},{parentUrl},{verifiedUrl}");
        }
    }
}