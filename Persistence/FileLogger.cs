using System;
using Helix.Persistence.Abstractions;

namespace Helix.Persistence
{
    public class FileLogger : ILogger
    {
        IFilePersistence _filePersistence;
        bool _objectDisposed;

        public FileLogger(string filePath)
        {
            _objectDisposed = false;
            _filePersistence = new FilePersistence(filePath);
        }

        public void Dispose()
        {
            if (_objectDisposed) return;
            _filePersistence?.Dispose();
            _filePersistence = null;
            _objectDisposed = true;
        }

        public virtual void LogException(Exception exception)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(FileLogger));
            _filePersistence.WriteLineAsync(exception.ToString());
        }

        public void LogInfo(string info)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(FileLogger));
            _filePersistence.WriteLineAsync($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [INFO] {info}");
        }
    }
}