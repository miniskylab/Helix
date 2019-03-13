using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Crawler
{
    public sealed class ReportWriter : IReportWriter
    {
        bool _objectDisposed;
        readonly Dictionary<string, object> _publicApiLockMap;
        readonly ISQLitePersistence<VerificationResult> _sqLitePersistence;
        IList<VerificationResult> _verificationResults;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ReportWriter(IPersistenceProvider persistenceProvider)
        {
            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var pathToDatabaseFile = Path.Combine(workingDirectory, "report.sqlite3");

            _objectDisposed = false;
            _verificationResults = new List<VerificationResult>();
            _sqLitePersistence = persistenceProvider.GetSQLitePersistence<VerificationResult>(pathToDatabaseFile);
            _publicApiLockMap = new Dictionary<string, object>
            {
                { $"{nameof(WriteReport)}", new object() },
                { $"{nameof(UpdateStatusCode)}", new object() }
            };
        }

        public void Dispose()
        {
            try
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Enter(lockObject);
                if (_objectDisposed) return;
                FlushMemoryBufferToDisk();
                _objectDisposed = true;
            }
            finally
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Exit(lockObject);
            }
        }

        public void UpdateStatusCode(int resourceId, StatusCode newStatusCode)
        {
            lock (_publicApiLockMap[nameof(UpdateStatusCode)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ReportWriter));
                var verificationResult = _sqLitePersistence.GetByPrimaryKey(resourceId);
                if (verificationResult != null)
                {
                    verificationResult.StatusCode = newStatusCode;
                    _sqLitePersistence.Update(verificationResult);
                    return;
                }

                verificationResult = _verificationResults.FirstOrDefault(dto => dto.Id == resourceId);
                if (verificationResult == null) throw new KeyNotFoundException();
                verificationResult.StatusCode = newStatusCode;
            }
        }

        public void WriteReport(VerificationResult verificationResult)
        {
            lock (_publicApiLockMap[nameof(WriteReport)])
            lock (_publicApiLockMap[nameof(UpdateStatusCode)])
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