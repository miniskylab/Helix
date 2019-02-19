using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;
using JetBrains.Annotations;

namespace Helix.Crawler
{
    public sealed class ReportWriter : IReportWriter
    {
        bool _objectDisposed;
        readonly Dictionary<string, object> _publicApiLockMap;
        ISQLitePersistence<VerificationResultDto> _sqLitePersistence;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ReportWriter(IPersistenceProvider persistenceProvider)
        {
            _objectDisposed = false;
            _sqLitePersistence = persistenceProvider.GetSQLitePersistence<VerificationResultDto>("report.db");
            _publicApiLockMap = new Dictionary<string, object> { { $"{nameof(WriteReport)}", new object() } };
        }

        public void Dispose()
        {
            try
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Enter(lockObject);
                if (_objectDisposed) return;
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
                _objectDisposed = true;
            }
            finally
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Exit(lockObject);
            }
        }

        public void WriteReport(VerificationResult verificationResult)
        {
            lock (_publicApiLockMap[nameof(WriteReport)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ReportWriter));
                _sqLitePersistence.Save(new VerificationResultDto
                {
                    StatusCode = verificationResult.StatusCode,
                    VerifiedUrl = verificationResult.VerifiedUrl,
                    ParentUrl = verificationResult.ParentUrl,
                    IsBrokenResource = verificationResult.IsBrokenResource,
                    IsExtractedResource = verificationResult.IsExtractedResource,
                    IsInternalResource = verificationResult.IsInternalResource
                });
            }
        }

        void ReleaseUnmanagedResources()
        {
            _sqLitePersistence?.Dispose();
            _sqLitePersistence = null;
        }

        class VerificationResultDto
        {
            [Key]
            [UsedImplicitly]
            [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
            public int Id { get; set; }

            [Required]
            public bool IsBrokenResource { [UsedImplicitly] get; set; }

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