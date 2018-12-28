using System;
using System.IO;
using System.Reflection;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    sealed class ReportWriter : FilePersistence
    {
        static ReportWriter _instance;

        public static ReportWriter Instance =>
            _instance ?? (_instance = new ReportWriter($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Report.csv"));

        ReportWriter(string filePath, TimeSpan? flushDataToDiskInterval = null) : base(filePath, flushDataToDiskInterval)
        {
            WriteLineAsync("HTTP Status Code,Parent Url,Verified Url");
        }

        public void WriteReport(IVerificationResult verificationResult, bool writeBrokenLinksOnly = false)
        {
            if (writeBrokenLinksOnly && !verificationResult.IsBrokenResource) return;
            var parentUrl = verificationResult.Resource?.ParentUri?.OriginalString ?? verificationResult.RawResource.ParentUrl;
            var verifiedUrl = verificationResult.Resource?.Uri.OriginalString ?? verificationResult.RawResource.Url;
            WriteLineAsync($"{verificationResult.HttpStatusCode},{parentUrl},{verifiedUrl}");
        }
    }
}