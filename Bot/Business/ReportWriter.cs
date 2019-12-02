using System;
using System.Linq;
using Helix.Bot.Abstractions;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Helix.Bot
{
    public sealed class ReportWriter : IReportWriter
    {
        SqLiteDbContext _reportDatabaseContext;
        int _writingReportRequestCount;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ReportWriter(Configurations configurations)
        {
            _writingReportRequestCount = 0;

            _reportDatabaseContext = new SqLiteDbContext(configurations.PathToReportFile);
            _reportDatabaseContext.Database.EnsureDeleted();
            _reportDatabaseContext.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _reportDatabaseContext?.SaveChanges();
            _reportDatabaseContext?.Dispose();
            _reportDatabaseContext = null;
        }

        public void WriteReport(VerificationResult verificationResult)
        {
            try
            {
                _writingReportRequestCount++;

                var verificationResultStoredInDatabase = _reportDatabaseContext.VerificationResults
                    .SingleOrDefault(dbRecord => dbRecord.VerifiedUrl == verificationResult.VerifiedUrl);

                var duplicateUrlDetected = verificationResultStoredInDatabase != null;
                if (duplicateUrlDetected) _reportDatabaseContext.Remove(verificationResultStoredInDatabase);

                verificationResultStoredInDatabase = _reportDatabaseContext.VerificationResults
                    .SingleOrDefault(dbRecord => dbRecord.Id == verificationResult.Id);

                var duplicateIdDetected = verificationResultStoredInDatabase != null;
                if (duplicateIdDetected)
                {
                    _reportDatabaseContext.Entry(verificationResultStoredInDatabase).State = EntityState.Detached;
                    _reportDatabaseContext.Update(verificationResult);
                    // TODO: Update Statistics
                    return;
                }

                _reportDatabaseContext.Add(verificationResult);
            }
            finally
            {
                if (_writingReportRequestCount >= 300)
                {
                    _reportDatabaseContext.SaveChanges();
                    _writingReportRequestCount = 0;
                }
            }
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