using System.Collections.Generic;
using System.Threading.Tasks;
using Helix.Persistence.Abstractions;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Helix.Persistence
{
    class SQLitePersistence<TDto> : ISQLitePersistence<TDto> where TDto : class
    {
        readonly object _addToBufferSync;
        readonly object _flushToDiskSync;
        IList<TDto> _memoryBuffer;
        readonly string _pathToDatabaseFile;

        public SQLitePersistence(string pathToDatabaseFile)
        {
            _pathToDatabaseFile = pathToDatabaseFile;
            _memoryBuffer = new List<TDto>();
            _addToBufferSync = new object();
            _flushToDiskSync = new object();
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

        public void Save(TDto dto)
        {
            lock (_addToBufferSync)
            {
                if (_memoryBuffer.Count < 300) _memoryBuffer.Add(dto);
                else
                {
                    FlushToDatabaseFileOnDisk();
                    _memoryBuffer = new List<TDto> { dto };
                }
            }

            void FlushToDatabaseFileOnDisk()
            {
                var memoryBuffer = _memoryBuffer;
                Task.Run(() =>
                {
                    lock (_flushToDiskSync)
                    {
                        using (var sqLiteDbContext = new SQLiteDbContext(_pathToDatabaseFile))
                        {
                            sqLiteDbContext.DTOs.AddRange(memoryBuffer);
                            sqLiteDbContext.SaveChanges();
                        }
                    }
                });
            }
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