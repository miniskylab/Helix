using System;

namespace Helix.Persistence.Abstractions
{
    public interface IPersistenceProvider
    {
        IFilePersistence GetFilePersistence(string filePath, TimeSpan? flushDataToDiskInterval = null);

        ISqLitePersistence<TDto> GetSqLitePersistence<TDto>(string pathToDatabaseFile) where TDto : class;
    }
}