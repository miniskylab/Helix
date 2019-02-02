using System;

namespace Helix.Persistence.Abstractions
{
    public interface IFilePersistence : IDisposable
    {
        void WriteLineAsync(string text);
    }
}