using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Helix.Crawler
{
    class FilePersistence : IDisposable
    {
        readonly List<Task> _backgroundTasks;
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly string _filePath;
        TextWriter _textWriter;

        public FilePersistence(string filePath, TimeSpan? flushDataToDiskInterval = null)
        {
            _filePath = filePath;
            _cancellationTokenSource = new CancellationTokenSource();
            _backgroundTasks = new List<Task>();

            EnsureFileIsRecreated();
            FlushDataToDiskEvery(flushDataToDiskInterval ?? new TimeSpan(5));
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            Task.WhenAll(_backgroundTasks).Wait();
            _cancellationTokenSource?.Dispose();

            _textWriter?.Flush();
            _textWriter?.Dispose();
            _textWriter = null;
        }

        public void WriteLineAsync(string text) { _textWriter?.WriteLineAsync(text); }

        void EnsureFileIsRecreated()
        {
            if (_textWriter != null) return;
            if (File.Exists(_filePath)) File.Delete(_filePath);
            _textWriter = TextWriter.Synchronized(new StreamWriter(_filePath));
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
}