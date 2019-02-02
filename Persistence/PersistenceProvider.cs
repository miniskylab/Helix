using System;
using Helix.Core;
using Helix.Persistence.Abstractions;

namespace Helix.Persistence
{
    public class PersistenceProvider : IPersistenceProvider
    {
        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public PersistenceProvider() { }

        public IFilePersistence GetFilePersistence(string filePath, TimeSpan? flushDataToDiskInterval = null)
        {
            return new FilePersistence(filePath, flushDataToDiskInterval);
        }
    }
}