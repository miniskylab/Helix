using System;
using System.Threading;
using Helix.Crawler.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Crawler
{
    public class ReportWriterBlock : ActionBlock<VerificationResult>, IReportWriterBlock
    {
        readonly ILog _log;
        readonly IReportWriter _reportWriter;

        public ReportWriterBlock(CancellationToken cancellationToken, IReportWriter reportWriter, ILog log)
            : base(cancellationToken, maxDegreeOfParallelism: 300)
        {
            _log = log;
            _reportWriter = reportWriter;
        }

        protected override void Act(VerificationResult verificationResult)
        {
            try
            {
                if (verificationResult == null) throw new ArgumentNullException(nameof(verificationResult));
                _reportWriter.WriteReport(verificationResult);
            }
            catch (Exception exception)
            {
                _log.Error(
                    $"One or more errors occurred while saving to database: {JsonConvert.SerializeObject(verificationResult)}.",
                    exception
                );
            }
        }
    }
}