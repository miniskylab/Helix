using System;
using System.Collections.Generic;
using System.Linq;
using Helix.Bot.Abstractions;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Helix.Bot
{
    public sealed class ReportWriter : IReportWriter
    {
        readonly Configurations _configurations;
        List<VerificationResult> _memoryBuffer;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ReportWriter(Configurations configurations)
        {
            _configurations = configurations;
            _memoryBuffer = new List<VerificationResult>();

            using var reportDatabaseContext = new SqLiteDbContext(configurations.PathToReportFile);
            reportDatabaseContext.Database.EnsureDeleted();
            reportDatabaseContext.Database.EnsureCreated();
        }

        public void Dispose() { FlushMemoryBufferToDisk(); }

        public void Insert(params VerificationResult[] toBeInsertedVerificationResults)
        {
            if (_memoryBuffer.Count >= 300) FlushMemoryBufferToDisk();
            _memoryBuffer.AddRange(toBeInsertedVerificationResults);
        }

        public void Update(params VerificationResult[] verificationResults)
        {
            using var reportDatabaseContext = new SqLiteDbContext(_configurations.PathToReportFile);
            foreach (var verificationResult in verificationResults)
            {
                var trackedVerificationResult = _memoryBuffer.SingleOrDefault(WhereVerifiedUrlMatch) ??
                                                reportDatabaseContext.VerificationResults.Single(WhereVerifiedUrlMatch);

                trackedVerificationResult.IsInternalResource = verificationResult.IsInternalResource;
                trackedVerificationResult.StatusMessage = verificationResult.StatusMessage;
                trackedVerificationResult.ResourceType = verificationResult.ResourceType;
                trackedVerificationResult.StatusCode = verificationResult.StatusCode;

                #region Local Functions

                bool WhereVerifiedUrlMatch(VerificationResult v) => v.VerifiedUrl == verificationResult.VerifiedUrl;

                #endregion
            }
            reportDatabaseContext.SaveChanges();
        }

        void FlushMemoryBufferToDisk()
        {
            var memoryBuffer = _memoryBuffer;
            _memoryBuffer = new List<VerificationResult>();

            using var reportDatabaseContext = new SqLiteDbContext(_configurations.PathToReportFile);
            reportDatabaseContext.VerificationResults.AddRange(memoryBuffer);
            reportDatabaseContext.SaveChanges();
        }

        class SqLiteDbContext : DbContext
        {
            readonly string _pathToDatabaseFile;

            public DbSet<VerificationResult> VerificationResults { get; [UsedImplicitly] set; }

            public SqLiteDbContext(string pathToDatabaseFile) { _pathToDatabaseFile = pathToDatabaseFile; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlite(new SqliteConnectionStringBuilder { DataSource = _pathToDatabaseFile }.ToString());
            }

            protected override void OnModelCreating(ModelBuilder builder)
            {
                builder.Entity<VerificationResult>().HasIndex(u => u.VerifiedUrl).IsUnique();
            }
        }
    }
}