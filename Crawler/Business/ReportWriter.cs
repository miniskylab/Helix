using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;
using JetBrains.Annotations;

namespace Helix.Crawler
{
    public sealed class ReportWriter : IReportWriter
    {
        bool _objectDisposed;
        readonly Dictionary<string, object> _publicApiLockMap;
        readonly ISQLitePersistence<VerificationResultDataTransferObject> _sqLitePersistence;
        IList<VerificationResultDataTransferObject> _verificationResultDataTransferObjects;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ReportWriter(IPersistenceProvider persistenceProvider)
        {
            _objectDisposed = false;
            _sqLitePersistence = persistenceProvider.GetSQLitePersistence<VerificationResultDataTransferObject>("report.db");
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
                GC.SuppressFinalize(this);
                _objectDisposed = true;
            }
            finally
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Exit(lockObject);
            }
        }

        public void UpdateStatusCode(int resourceId, HttpStatusCode newStatusCode)
        {
            lock (_publicApiLockMap[nameof(UpdateStatusCode)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ReportWriter));
                var dataTransferObject = _verificationResultDataTransferObjects.FirstOrDefault(dto => dto.Id == resourceId) ??
                                         _sqLitePersistence.GetByPrimaryKey(resourceId) ??
                                         throw new KeyNotFoundException();

                dataTransferObject.StatusCode = newStatusCode;
                _sqLitePersistence.Update(dataTransferObject);
            }
        }

        public void WriteReport(VerificationResult verificationResult)
        {
            lock (_publicApiLockMap[nameof(WriteReport)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ReportWriter));
                if (_verificationResultDataTransferObjects.Count >= 300) FlushMemoryBufferToDisk();
                _verificationResultDataTransferObjects.Add(new VerificationResultDataTransferObject
                {
                    Id = verificationResult.Resource.Id,
                    StatusCode = verificationResult.StatusCode,
                    VerifiedUrl = verificationResult.VerifiedUrl,
                    ParentUrl = verificationResult.ParentUrl,
                    IsExtractedResource = verificationResult.IsExtractedResource,
                    IsInternalResource = verificationResult.IsInternalResource
                });
            }
        }

        void FlushMemoryBufferToDisk()
        {
            var memoryBuffer = _verificationResultDataTransferObjects;
            Task.Run(() => { _sqLitePersistence.Save(memoryBuffer.ToArray()); });
            _verificationResultDataTransferObjects = new List<VerificationResultDataTransferObject>();
        }

        class VerificationResultDataTransferObject
        {
            [Key]
            public int Id { [UsedImplicitly] get; set; }

            [Required]
            public bool IsExtractedResource { [UsedImplicitly] get; set; }

            [Required]
            public bool IsInternalResource { [UsedImplicitly] get; set; }

            public string ParentUrl { [UsedImplicitly] get; set; }

            [Required]
            public HttpStatusCode StatusCode { [UsedImplicitly] get; set; }

            [Required]
            public string VerifiedUrl { [UsedImplicitly] get; set; }
        }

        ~ReportWriter() { Dispose(); }
    }
}