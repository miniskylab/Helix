using System;
using Helix.Persistence.Abstractions;

namespace Helix.Persistence
{
    public class PersistenceProvider : IPersistenceProvider
    {
        public IFilePersistence GetFilePersistence(string filePath, TimeSpan? flushDataToDiskInterval = null)
        {
            return new FilePersistence(filePath, flushDataToDiskInterval);
        }

        public ISQLitePersistence<TDto> GetSQLitePersistence<TDto>(string pathToDatabaseFile) where TDto : class
        {
            return new SQLitePersistence<TDto>(pathToDatabaseFile);
        }
    }
}