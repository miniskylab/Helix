using System;
using Helix.Bot.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Bot
{
    public class ReportWriterBlock : ActionBlock<VerificationResult>, IReportWriterBlock
    {
        public ReportWriterBlock(IReportWriter reportWriter, ILog log) : base(true)
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
                    $"One or more errors occurred while saving to database: {JsonConvert.SerializeObject(verificationResult)}.",
                    exception
                );
            }
        }

        #region Injected Services

        readonly ILog _log;
        readonly IReportWriter _reportWriter;

        #endregion
    }
}