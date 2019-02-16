using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;
using JetBrains.Annotations;

namespace Helix.Crawler
{
    public sealed class ReportWriter : IReportWriter
    {
        readonly Configurations _configurations;
        readonly ISQLitePersistence<VerificationResultDto> _sqLitePersistence;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ReportWriter(Configurations configurations, IPersistenceProvider persistenceProvider)
        {
            _configurations = configurations;
            _sqLitePersistence = persistenceProvider.GetSQLitePersistence<VerificationResultDto>("report.db");
        }

        public void WriteReport(VerificationResult verificationResult)
        {
            if (_configurations.ReportBrokenLinksOnly && !verificationResult.IsBrokenResource) return;
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
    }
}