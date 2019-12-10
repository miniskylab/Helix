using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Helix.Bot.Abstractions;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;

namespace Helix.Bot
{
    public sealed class ReportWriter : IReportWriter
    {
        List<VerificationResult> _memoryBuffer;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ReportWriter(Configurations configurations, IProcessedUrlRegister processedUrlRegister)
        {
            _configurations = configurations;
            _processedUrlRegister = processedUrlRegister;
            _memoryBuffer = new List<VerificationResult>();

            using var reportDatabaseContext = new SqLiteDbContext(configurations.PathToReportFile);
            reportDatabaseContext.Database.EnsureDeleted();
            reportDatabaseContext.Database.EnsureCreated();
        }

        public void AddNew(params VerificationResult[] toBeAddedVerificationResults)
        {
            if (_memoryBuffer.Count >= 300) FlushMemoryBufferToDisk();
            foreach (var toBeAddedVerificationResult in toBeAddedVerificationResults)
            {
                _processedUrlRegister.MarkAsSavedToReportFile(toBeAddedVerificationResult.VerifiedUrl);
                _memoryBuffer.Add(toBeAddedVerificationResult);
            }
        }

        public void Dispose() { FlushMemoryBufferToDisk(); }

        public void RemoveAndUpdate(params VerificationResult[] toBeUpdatedVerificationResults)
        {
            using var reportDatabaseContext = new SqLiteDbContext(_configurations.PathToReportFile);
            reportDatabaseContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            foreach (var toBeUpdatedVerificationResult in toBeUpdatedVerificationResults)
            {
                Remove();
                Update();

                #region Local Functions

                void Remove()
                {
                    while (!_processedUrlRegister.IsSavedToReportFile(toBeUpdatedVerificationResult.VerifiedUrl))
                        Thread.Sleep(500);

                    var trackedVerificationResult = _memoryBuffer.SingleOrDefault(WhereVerifiedUrlMatch);
                    if (trackedVerificationResult != null) _memoryBuffer.Remove(trackedVerificationResult);
                    else
                    {
                        trackedVerificationResult = reportDatabaseContext.VerificationResults.SingleOrDefault(WhereVerifiedUrlMatch);
                        if (trackedVerificationResult != null) reportDatabaseContext.VerificationResults.Remove(trackedVerificationResult);
                        else
                        {
                            var errorMessage = $"Cannot find any {nameof(VerificationResult)} " +
                                               $"sharing {nameof(VerificationResult.VerifiedUrl)} " +
                                               $"with: {toBeUpdatedVerificationResult.ToJson()}";
                            throw new NotFoundException(errorMessage);
                        }
                    }

                    #region Local Functions

                    bool WhereVerifiedUrlMatch(VerificationResult v) => v.VerifiedUrl == toBeUpdatedVerificationResult.VerifiedUrl;

                    #endregion
                }
                void Update()
                {
                    var bufferedVerificationResult = _memoryBuffer.SingleOrDefault(v => v.Id == toBeUpdatedVerificationResult.Id);
                    if (bufferedVerificationResult != null)
                    {
                        _memoryBuffer.Remove(bufferedVerificationResult);
                        _memoryBuffer.Add(toBeUpdatedVerificationResult);
                    }
                    else
                        reportDatabaseContext.VerificationResults.Update(toBeUpdatedVerificationResult);
                }

                #endregion
            }
            reportDatabaseContext.SaveChanges();
        }

        public void Update(params VerificationResult[] toBeUpdatedVerificationResults)
        {
            foreach (var toBeUpdatedVerificationResult in toBeUpdatedVerificationResults)
            {
                var bufferedVerificationResult = _memoryBuffer.SingleOrDefault(v => v.Id == toBeUpdatedVerificationResult.Id);
                if (bufferedVerificationResult != null)
                {
                    _memoryBuffer.Remove(bufferedVerificationResult);
                    _memoryBuffer.Add(toBeUpdatedVerificationResult);
                    continue;
                }

                using var reportDatabaseContext = new SqLiteDbContext(_configurations.PathToReportFile);
                reportDatabaseContext.VerificationResults.Update(toBeUpdatedVerificationResult);
                reportDatabaseContext.SaveChanges();
            }
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

        readonly Configurations _configurations;
        readonly IProcessedUrlRegister _processedUrlRegister;

        #endregion
    }
}