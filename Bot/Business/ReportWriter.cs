using System;
using System.Collections.Generic;
using System.Linq;
using Helix.Bot.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Bot
{
    public sealed class ReportWriter : IReportWriter
    {
        bool _objectDisposed;
        readonly ISqLitePersistence<VerificationResult> _sqLitePersistence;
        IList<VerificationResult> _verificationResults;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ReportWriter(ISqLitePersistence<VerificationResult> sqLitePersistence)
        {
            _objectDisposed = false;
            _sqLitePersistence = sqLitePersistence;
            _verificationResults = new List<VerificationResult>();
        }

        public void Dispose()
        {
            if (_objectDisposed) return;
            FlushMemoryBufferToDisk();
            _objectDisposed = true;
        }

        public void WriteReport(VerificationResult verificationResult)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ReportWriter));
            if (_verificationResults.Count >= 300) FlushMemoryBufferToDisk();

            var oldVerificationResult = _verificationResults.FirstOrDefault(v => v.Id == verificationResult.Id);
            if (oldVerificationResult != null)
            {
                _verificationResults.Remove(oldVerificationResult);
                _verificationResults.Add(verificationResult);
                return;
            }

            oldVerificationResult = _sqLitePersistence.GetByPrimaryKey(verificationResult.Id);
            if (oldVerificationResult != null)
            {
                _sqLitePersistence.Update(verificationResult);
                return;
            }

            _verificationResults.Add(verificationResult);
        }

        void FlushMemoryBufferToDisk()
        {
            var verificationResults = _verificationResults;
            _sqLitePersistence.Save(verificationResults.ToArray());
            _verificationResults = new List<VerificationResult>();
        }
    }
}