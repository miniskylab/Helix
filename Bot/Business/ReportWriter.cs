using System;
using System.Linq;
using Helix.Bot.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Bot
{
    public sealed class ReportWriter : IReportWriter
    {
        readonly ISqLitePersistence<VerificationResult> _sqLitePersistence;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ReportWriter(ISqLitePersistence<VerificationResult> sqLitePersistence) { _sqLitePersistence = sqLitePersistence; }

        public void WriteReport(VerificationResult verificationResult)
        {
            if (_sqLitePersistence == null) throw new ObjectDisposedException(nameof(ReportWriter));

            var verificationResultFromDatabase = _sqLitePersistence
                .Select(dbRecord => dbRecord.VerifiedUrl == verificationResult.VerifiedUrl)
                .FirstOrDefault();

            var duplicateVerifiedUrlDetected = verificationResultFromDatabase != null;
            if (duplicateVerifiedUrlDetected) _sqLitePersistence.Delete(verificationResultFromDatabase);

            verificationResultFromDatabase = _sqLitePersistence
                .Select(dbRecord => dbRecord.Id == verificationResult.Id)
                .FirstOrDefault();

            var duplicateIdDetected = verificationResultFromDatabase != null;
            if (duplicateIdDetected)
            {
                _sqLitePersistence.Update(verificationResult);
                return;
            }

            _sqLitePersistence.Insert(verificationResult);
        }
    }
}