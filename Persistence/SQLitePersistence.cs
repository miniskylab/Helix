using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Helix.Persistence.Abstractions;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Helix.Persistence
{
    class SQLitePersistence<TDto> : ISQLitePersistence<TDto> where TDto : class
    {
        readonly object _flushToDatabaseFileOnDiskLock;
        IList<TDto> _memoryBuffer;
        bool _objectDisposed;
        readonly string _pathToDatabaseFile;
        readonly Dictionary<string, object> _publicApiLockMap;

        public SQLitePersistence(string pathToDatabaseFile)
        {
            _memoryBuffer = new List<TDto>();
            _objectDisposed = false;
            _pathToDatabaseFile = pathToDatabaseFile;
            _flushToDatabaseFileOnDiskLock = new object();
            _publicApiLockMap = new Dictionary<string, object> { { $"{nameof(Save)}", new object() } };
            EnsureDatabaseIsRecreated();

            void EnsureDatabaseIsRecreated()
            {
                using (var reportDatabaseContext = new SQLiteDbContext(pathToDatabaseFile))
                {
                    reportDatabaseContext.Database.EnsureDeleted();
                    reportDatabaseContext.Database.EnsureCreated();
                }
            }
        }

        public void Dispose()
        {
            try
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Enter(lockObject);
                if (_objectDisposed) return;
                FlushToDatabaseFileOnDisk();
                _objectDisposed = true;
            }
            finally
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Exit(lockObject);
            }
        }

        public void Save(TDto dto)
        {
            lock (_publicApiLockMap[nameof(Save)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(SQLitePersistence<TDto>));
                if (_memoryBuffer.Count >= 300) FlushToDatabaseFileOnDisk();
                _memoryBuffer.Add(dto);
            }
        }

        void FlushToDatabaseFileOnDisk()
        {
            var memoryBuffer = _memoryBuffer;
            Task.Run(() =>
            {
                lock (_flushToDatabaseFileOnDiskLock)
                {
                    using (var sqLiteDbContext = new SQLiteDbContext(_pathToDatabaseFile))
                    {
                        sqLiteDbContext.DTOs.AddRange(memoryBuffer);
                        sqLiteDbContext.SaveChanges();
                    }
                }
            });
            _memoryBuffer = new List<TDto>();
        }

        class SQLiteDbContext : DbContext
        {
            readonly string _pathToDatabaseFile;

            public DbSet<TDto> DTOs { get; [UsedImplicitly] set; }

            public SQLiteDbContext(string pathToDatabaseFile) { _pathToDatabaseFile = pathToDatabaseFile; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlite(new SqliteConnectionStringBuilder { DataSource = _pathToDatabaseFile }.ToString());
            }
        }
    }
}