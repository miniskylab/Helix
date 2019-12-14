using System;
using System.Collections.Generic;
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

        public void WriteReport(params VerificationResult[] verificationResults)
        {
            if (_memoryBuffer.Count >= 300) FlushMemoryBufferToDisk();
            _memoryBuffer.AddRange(verificationResults);
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