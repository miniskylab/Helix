using System;
using System.Threading;
using Helix.Crawler.Abstractions;
using log4net;

namespace Helix.Crawler
{
    public class ReportWriterBlock : ActionBlock<VerificationResult>
    {
        readonly ILog _log;
        readonly IReportWriter _reportWriter;

        public ReportWriterBlock(CancellationToken cancellationToken, IReportWriter reportWriter, ILog log) : base(cancellationToken)
        {
            _log = log;
            _reportWriter = reportWriter;
        }

        protected override void Act(VerificationResult verificationResult)
        {
            try { _reportWriter.WriteReport(verificationResult); }
            catch (Exception exception)
            {
                _log.Error(
                    $"One or more errors occurred while saving this URL to database: {verificationResult.VerifiedUrl}.",
                    exception
                );
            }
        }
    }
}