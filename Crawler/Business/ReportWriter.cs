using System;
using System.Collections.Generic;
using System.IO;
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
        public ReportWriter(Configurations configurations, IPersistenceProvider persistenceProvider)
        {
            _objectDisposed = false;
            _writeLock = new object();
            _verificationResults = new List<VerificationResult>();

            var pathToDatabaseFile = Path.Combine(configurations.WorkingDirectory, "report.sqlite3");
            _sqLitePersistence = persistenceProvider.GetSqLitePersistence<VerificationResult>(pathToDatabaseFile);
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

        public void UpdateStatusCode(int resourceId, StatusCode newStatusCode)
        {
            lock (_writeLock)
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ReportWriter));
                var verificationResult = _verificationResults.FirstOrDefault(v => v.Id == resourceId);
                if (verificationResult != null)
                {
                    verificationResult.StatusCode = newStatusCode;
                    return;
                }

                verificationResult = _sqLitePersistence.GetByPrimaryKey(resourceId);
                if (verificationResult == null) throw new KeyNotFoundException();
                verificationResult.StatusCode = newStatusCode;
                _sqLitePersistence.Update(verificationResult);
            }
        }

        public void WriteReport(VerificationResult verificationResult)
        {
            lock (_writeLock)
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ReportWriter));
                if (_verificationResults.Count >= 300) FlushMemoryBufferToDisk();
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