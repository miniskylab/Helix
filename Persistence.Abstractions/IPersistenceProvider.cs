using System;

namespace Helix.Persistence.Abstractions
{
    public interface IPersistenceProvider
    {
        IFilePersistence GetFilePersistence(string filePath, TimeSpan? flushDataToDiskInterval = null);

        ISQLitePersistence<TDto> GetSQLitePersistence<TDto>(string pathToDatabaseFile) where TDto : class;
    }
}