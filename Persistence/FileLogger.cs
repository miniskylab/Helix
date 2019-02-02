using System;
using Helix.Persistence.Abstractions;

namespace Helix.Persistence
{
    public class FileLogger : ILogger
    {
        readonly FilePersistence _filePersistence;

        public FileLogger(string filePath) { _filePersistence = new FilePersistence(filePath); }

        public void Dispose() { _filePersistence?.Dispose(); }

        public virtual void LogException(Exception exception) { _filePersistence.WriteLineAsync(exception.ToString()); }

        public void LogInfo(string info) { _filePersistence.WriteLineAsync($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [INFO] {info}"); }
    }
}