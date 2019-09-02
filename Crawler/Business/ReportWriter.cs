using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Crawler
{
    public sealed class ReportWriter : IReportWriter
    {
        bool _objectDisposed;
        readonly ISqLitePersistence<VerificationResult> _sqLitePersistence;
        IList<VerificationResult> _verificationResults;
        readonly object _writeLock;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ReportWriter(ISqLitePersistence<VerificationResult> sqLitePersistence)
        {
            _objectDisposed = false;
            _writeLock = new object();
            _sqLitePersistence = sqLitePersistence;
            _verificationResults = new List<VerificationResult>();
        }

        public void Dispose()
        {
            lock (_writeLock)
            {
                if (_objectDisposed) return;
                FlushMemoryBufferToDisk();
                _objectDisposed = true;
            }
        }

        public void WriteReport(VerificationResult verificationResult)
        {
            lock (_writeLock)
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
        }

        void FlushMemoryBufferToDisk()
        {
            var verificationResults = _verificationResults;
            Task.Run(() => { _sqLitePersistence.Save(verificationResults.ToArray()); });
            _verificationResults = new List<VerificationResult>();
        }
    }
}