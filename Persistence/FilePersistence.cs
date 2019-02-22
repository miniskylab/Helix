using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Helix.Persistence.Abstractions;

namespace Helix.Persistence
{
    class FilePersistence : IFilePersistence
    {
        readonly List<Task> _backgroundTasks;
        CancellationTokenSource _cancellationTokenSource;
        bool _objectDisposed;
        readonly Dictionary<string, object> _publicApiLockMap;
        TextWriter _textWriter;

        public FilePersistence(string filePath, TimeSpan? flushDataToDiskInterval = null)
        {
            _objectDisposed = false;
            _backgroundTasks = new List<Task>();
            _cancellationTokenSource = new CancellationTokenSource();
            _publicApiLockMap = new Dictionary<string, object> { { $"{nameof(WriteLineAsync)}", new object() } };

            EnsureFileIsRecreated();
            FlushDataToDiskEvery(flushDataToDiskInterval ?? TimeSpan.FromSeconds(3));

            void EnsureFileIsRecreated()
            {
                if (_textWriter != null) return;
                if (File.Exists(filePath)) File.Delete(filePath);
                _textWriter = TextWriter.Synchronized(new StreamWriter(filePath));
            }
            void FlushDataToDiskEvery(TimeSpan timeSpan)
            {
                _backgroundTasks.Add(Task.Run(() =>
                {
                    while (_textWriter != null && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        _textWriter?.Flush();
                        Thread.Sleep(timeSpan);
                    }
                }, _cancellationTokenSource.Token));
            }
        }

        public void Dispose()
        {
            try
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Enter(lockObject);
                if (_objectDisposed) return;
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
                _objectDisposed = true;
            }
            finally
            {
                foreach (var lockObject in _publicApiLockMap.Values) Monitor.Exit(lockObject);
            }
        }

        public void WriteLineAsync(string text)
        {
            lock (_publicApiLockMap[nameof(WriteLineAsync)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(FilePersistence));
                _textWriter?.WriteLineAsync(text);
            }
        }

        void ReleaseUnmanagedResources()
        {
            _cancellationTokenSource?.Cancel();
            Task.WhenAll(_backgroundTasks).Wait();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _textWriter?.Flush();
            _textWriter?.Dispose();
            _textWriter = null;
        }

        ~FilePersistence() { ReleaseUnmanagedResources(); }
    }
}