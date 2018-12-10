using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    sealed class ReportWriter
    {
        readonly List<Task> _backgroundTasks;
        readonly CancellationTokenSource _cancellationTokenSource;
        static ReportWriter _instance;
        TextWriter _textWriter;

        public static ReportWriter Instance => _instance ?? (_instance = new ReportWriter());

        ReportWriter()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _backgroundTasks = new List<Task>();

            EnsureReportFileIsRecreated();
            FlushDataToDiskEvery(TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            Task.WhenAll(_backgroundTasks).Wait();
            _cancellationTokenSource?.Dispose();

            _textWriter?.Flush();
            _textWriter?.Dispose();
            _instance = null;
        }

        public void WriteReport(IVerificationResult verificationResult, bool writeBrokenLinksOnly = false)
        {
            if (writeBrokenLinksOnly && !verificationResult.IsBrokenResource) return;
            var parentUrl = verificationResult.Resource?.ParentUri?.OriginalString ?? verificationResult.RawResource.ParentUrl;
            var verifiedUrl = verificationResult.Resource?.Uri.OriginalString ?? verificationResult.RawResource.Url;
            _textWriter.WriteLineAsync($"{verificationResult.HttpStatusCode},{parentUrl},{verifiedUrl}");
        }

        void EnsureReportFileIsRecreated()
        {
            if (_textWriter != null) return;
            var reportFilePath = $@"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}\Report.csv";
            if (File.Exists(reportFilePath)) File.Delete(reportFilePath);
            _textWriter = TextWriter.Synchronized(new StreamWriter(reportFilePath));
            _textWriter.WriteLineAsync("HTTP Status Code,Parent Url,Verified Url");
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