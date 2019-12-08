using System;
using Helix.Bot.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Bot
{
    public class ReportWriterBlock : ActionBlock<(ReportWritingAction, VerificationResult)>, IReportWriterBlock
    {
        public ReportWriterBlock(IReportWriter reportWriter, ILog log) : base(true)
        {
            _log = log;
            _reportWriter = reportWriter;
        }

        protected override void Act((ReportWritingAction, VerificationResult) _)
        {
            var (reportWritingAction, verificationResult) = _;
            try
            {
                if (verificationResult == null) throw new ArgumentNullException(nameof(verificationResult));
                switch (reportWritingAction)
                {
                    case ReportWritingAction.AddNew:
                        _reportWriter.AddNew(verificationResult);
                        break;
                    case ReportWritingAction.Update:
                        _reportWriter.Update(verificationResult);
                        break;
                    case ReportWritingAction.RemoveAndUpdate:
                        _reportWriter.RemoveAndUpdate(verificationResult);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(reportWritingAction));
                }
            }
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