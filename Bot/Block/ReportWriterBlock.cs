using System;
using Helix.Bot.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Bot
{
    public class ReportWriterBlock : ActionBlock<(ReportWritingAction, VerificationResult)>, IReportWriterBlock
    {
        public ReportWriterBlock(IReportWriter reportWriter, ILog log)
        {
            _log = log;
            _reportWriter = reportWriter;
        }

        protected override void Act((ReportWritingAction, VerificationResult) _)
        {
            var (reportWritingAction, verificationResult) = _;
            try
            {
                switch (reportWritingAction)
                {
                    case ReportWritingAction.AddNew:
                        _reportWriter.Insert(verificationResult);
                        break;
                    case ReportWritingAction.Update:
                        _reportWriter.Update(verificationResult);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception exception)
            {
                var reportWritingActionName = Enum.GetName(typeof(ReportWritingAction), reportWritingAction);
                var verificationResultJson = JsonConvert.SerializeObject(verificationResult);
                _log.Error(
                    $"One or more errors occurred while doing {reportWritingActionName} action on: {verificationResultJson}.",
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