using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Helix.Core;
using Helix.Crawler.Abstractions;
using JetBrains.Annotations;

namespace Helix.Crawler
{
    public sealed class Memory : IMemory
    {
        int _activeThreadCount;
        readonly ConcurrentSet<string> _alreadyVerifiedUrls = new ConcurrentSet<string>();
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly BlockingCollection<HtmlDocument> _toBeExtractedHtmlDocuments = new BlockingCollection<HtmlDocument>();
        readonly BlockingCollection<Uri> _toBeRenderedUris = new BlockingCollection<Uri>();
        readonly BlockingCollection<RawResource> _toBeVerifiedRawResources = new BlockingCollection<RawResource>();
        readonly string _workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        static readonly object StaticLock = new object();

        public Configurations Configurations { get; }

        public CrawlerState CrawlerState { get; private set; } = CrawlerState.Ready;

        public string ErrorFilePath { get; }

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public bool EverythingIsDone => !_toBeVerifiedRawResources.Any() && !_toBeRenderedUris.Any() && _activeThreadCount == 0;

        public int RemainingUrlCount => _activeThreadCount + _toBeVerifiedRawResources.Count;

        public Memory(Configurations configurations)
        {
            Configurations = configurations;
            ErrorFilePath = Path.Combine(_workingDirectory, "errors.txt");
            _cancellationTokenSource = new CancellationTokenSource();
            Interlocked.Exchange(ref _activeThreadCount, 0);
            while (_toBeVerifiedRawResources.Any()) _toBeVerifiedRawResources.Take();
            lock (StaticLock)
            {
                _alreadyVerifiedUrls.Clear();
                _alreadyVerifiedUrls.Add(Configurations.StartUrl);
                _toBeVerifiedRawResources.Add(new RawResource { Url = Configurations.StartUrl, ParentUri = null }, CancellationToken);
            }
        }

        [UsedImplicitly]
        public Memory() { }

        public void CancelEverything() { _cancellationTokenSource.Cancel(); }

        public void DecrementActiveThreadCount() { Interlocked.Decrement(ref _activeThreadCount); }

        public void IncrementActiveThreadCount() { Interlocked.Increment(ref _activeThreadCount); }

        public void Memorize(RawResource toBeVerifiedRawResource)
        {
            if (CancellationToken.IsCancellationRequested) return;
            lock (StaticLock)
            {
                if (_alreadyVerifiedUrls.Contains(toBeVerifiedRawResource.Url.StripFragment())) return;
                _alreadyVerifiedUrls.Add(toBeVerifiedRawResource.Url.StripFragment());
            }

            try { _toBeVerifiedRawResources.Add(toBeVerifiedRawResource, CancellationToken); }
            catch (OperationCanceledException operationCanceledException)
            {
                if (operationCanceledException.CancellationToken != CancellationToken) throw;
            }
        }

        public void Memorize(Uri toBeRenderedUri)
        {
            try { _toBeRenderedUris.Add(toBeRenderedUri, CancellationToken); }
            catch (OperationCanceledException operationCanceledException)
            {
                if (operationCanceledException.CancellationToken != CancellationToken) throw;
            }
        }

        public void Memorize(HtmlDocument toBeExtractedHtmlDocument)
        {
            try { _toBeExtractedHtmlDocuments.Add(toBeExtractedHtmlDocument, CancellationToken); }
            catch (OperationCanceledException operationCanceledException)
            {
                if (operationCanceledException.CancellationToken != CancellationToken) throw;
            }
        }

        public HtmlDocument TakeToBeExtractedHtmlDocument() { return _toBeExtractedHtmlDocuments.Take(CancellationToken); }

        public Uri TakeToBeRenderedUri() { return _toBeRenderedUris.Take(CancellationToken); }

        public RawResource TakeToBeVerifiedRawResource() { return _toBeVerifiedRawResources.Take(); }

        public bool TryTransitTo(CrawlerState crawlerState)
        {
            if (CrawlerState == CrawlerState.Unknown) return false;
            switch (crawlerState)
            {
                case CrawlerState.Ready:
                    lock (StaticLock)
                    {
                        if (CrawlerState != CrawlerState.Stopping) return false;
                        CrawlerState = CrawlerState.Ready;
                        return true;
                    }
                case CrawlerState.Working:
                    lock (StaticLock)
                    {
                        if (CrawlerState != CrawlerState.Ready && CrawlerState != CrawlerState.Paused) return false;
                        CrawlerState = CrawlerState.Working;
                        return true;
                    }
                case CrawlerState.Stopping:
                    lock (StaticLock)
                    {
                        if (CrawlerState != CrawlerState.Working && CrawlerState != CrawlerState.Paused) return false;
                        CrawlerState = CrawlerState.Stopping;
                        return true;
                    }
                case CrawlerState.Paused:
                    lock (StaticLock)
                    {
                        if (CrawlerState != CrawlerState.Working) return false;
                        CrawlerState = CrawlerState.Paused;
                        return true;
                    }
                case CrawlerState.Unknown:
                    throw new NotSupportedException($"Cannot transit to [{nameof(CrawlerState.Unknown)}] state.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(crawlerState), crawlerState, null);
            }
        }

        ~Memory()
        {
            _cancellationTokenSource?.Dispose();
            _toBeVerifiedRawResources?.Dispose();
        }
    }
}