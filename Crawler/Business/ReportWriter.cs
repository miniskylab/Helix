using System;
using System.IO;
using System.Reflection;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    // TODO: Singleton via Dependency Injection
    sealed class ReportWriter : FilePersistence
    {
        static ReportWriter _instance;

        public static ReportWriter Instance =>
            _instance ?? (_instance = new ReportWriter($@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Report.csv"));

        ReportWriter(string filePath, TimeSpan? flushDataToDiskInterval = null) : base(filePath, flushDataToDiskInterval)
        {
            WriteLineAsync("HTTP Status Code,Parent Url,Verified Url");
        }

        public void WriteReport(VerificationResult verificationResult, bool writeBrokenLinksOnly = false)
        {
            if (writeBrokenLinksOnly && !verificationResult.IsBrokenResource) return;
            var parentUri = verificationResult.Resource?.ParentUri;
            var verifiedUrl = verificationResult.Resource?.Uri.OriginalString ?? verificationResult.RawResource.Url;
            WriteLineAsync($"{verificationResult.HttpStatusCode},{parentUri?.OriginalString},{verifiedUrl}");
        }
    }
}