using Helix.Persistence.Abstractions;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Helix.Persistence
{
    // TODO: Add buffer to boost performance, avoid hitting disk too often.
    class SQLitePersistence<TDto> : ISQLitePersistence<TDto> where TDto : class
    {
        readonly string _pathToDatabaseFile;

        public SQLitePersistence(string pathToDatabaseFile)
        {
            _pathToDatabaseFile = pathToDatabaseFile;
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
            using (var sqLiteDbContext = new SQLiteDbContext(_pathToDatabaseFile))
            {
                sqLiteDbContext.DTOs.Add(dto);
                sqLiteDbContext.SaveChanges();
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