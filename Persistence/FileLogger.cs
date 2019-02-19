using System;
using System.Collections.Generic;
using System.Threading;
using Helix.Persistence.Abstractions;

namespace Helix.Persistence
{
    public class FileLogger : ILogger
    {
        IFilePersistence _filePersistence;
        bool _objectDisposed;
        readonly Dictionary<string, object> _publicApiLockMap;

        public FileLogger(string filePath)
        {
            _objectDisposed = false;
            _filePersistence = new FilePersistence(filePath);
            _publicApiLockMap = new Dictionary<string, object>
            {
                { $"{nameof(LogException)}", new object() },
                { $"{nameof(LogInfo)}", new object() }
            };
        }

        public void Dispose()
        {
            try
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Enter(lockObject);
                if (_objectDisposed) return;
                _filePersistence?.Dispose();
                _filePersistence = null;
                _objectDisposed = true;
            }
            finally
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Exit(lockObject);
            }
        }

        public virtual void LogException(Exception exception)
        {
            lock (_publicApiLockMap[nameof(LogException)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(FileLogger));
                _filePersistence.WriteLineAsync(exception.ToString());
            }
        }

        public void LogInfo(string info)
        {
            lock (_publicApiLockMap[nameof(LogInfo)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(FileLogger));
                _filePersistence.WriteLineAsync($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [INFO] {info}");
            }
        }
    }
}