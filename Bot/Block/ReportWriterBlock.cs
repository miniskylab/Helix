using System;
using System.Collections.Generic;
using System.Linq;
using Helix.Bot.Abstractions;
using JetBrains.Annotations;
using log4net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Helix.Bot
{
    public class ReportWriterBlock : ActionBlock<(ReportWritingAction, VerificationResult[])>, IReportWriterBlock
    {
        List<VerificationResult> _memoryBuffer;

        public ReportWriterBlock(Configurations configurations, ILog log)
        {
            _log = log;
            _configurations = configurations;
            _memoryBuffer = new List<VerificationResult>();

            using var reportDatabaseContext = new SqLiteDbContext(configurations.PathToReportFile);
            reportDatabaseContext.Database.EnsureDeleted();
            reportDatabaseContext.Database.EnsureCreated();
        }

        public void Dispose() { FlushMemoryBufferToDisk(); }

        protected override void Act((ReportWritingAction, VerificationResult[]) _)
        {
            var (reportWritingAction, verificationResults) = _;
            try
            {
                switch (reportWritingAction)
                {
                    case ReportWritingAction.AddNew:
                        Insert(verificationResults);
                        break;
                    case ReportWritingAction.Update:
                        Update(verificationResults);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception exception)
            {
                var reportWritingActionName = Enum.GetName(typeof(ReportWritingAction), reportWritingAction);
                var verificationResultJson = JsonConvert.SerializeObject(verificationResults);
                _log.Error(
                    $"One or more errors occurred while doing {reportWritingActionName} action on: {verificationResultJson}.",
                    exception
                );
            }

            #region Local Functions

            void Insert(params VerificationResult[] toBeInsertedVerificationResults)
            {
                _memoryBuffer.AddRange(toBeInsertedVerificationResults);
                if (_memoryBuffer.Count >= 300 || toBeInsertedVerificationResults.Any(InternalAndBroken))
                    FlushMemoryBufferToDisk();

                #region Local Function

                static bool InternalAndBroken(VerificationResult verificationResult)
                {
                    var @internal = verificationResult.IsInternalResource;
                    var broken = verificationResult.StatusCode.IsWithinBrokenRange();
                    return @internal && broken;
                }

                #endregion
            }
            void Update(params VerificationResult[] toBeUpdatedVerificationResults)
            {
                using var reportDatabaseContext = new SqLiteDbContext(_configurations.PathToReportFile);
                foreach (var verificationResult in toBeUpdatedVerificationResults)
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

            #endregion
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

        #region Injected Services

        readonly ILog _log;
        readonly Configurations _configurations;

        #endregion
    }
}