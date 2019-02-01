using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class Management : IManagement
    {
        const int RawResourceExtractorCount = 300;
        const int RawResourceVerifierCount = 2500;
        CancellationTokenSource _cancellationTokenSource;
        readonly object _extractionSyncRoot = new object();
        readonly IMemory _memory;
        int _pendingExtractionTaskCount;
        int _pendingRenderingTaskCount;
        int _pendingVerificationTaskCount;
        BlockingCollection<IRawResourceExtractor> _rawResourceExtractorPool;
        BlockingCollection<IRawResourceVerifier> _rawResourceVerifierPool;
        readonly object _renderingSyncRoot = new object();
        readonly object _syncRoot = new object();
        readonly object _verificationSyncRoot = new object();
        BlockingCollection<IWebBrowser> _webBrowserPool;

        public CrawlerState CrawlerState { get; private set; } = CrawlerState.Ready;

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public bool EverythingIsDone
        {
            get
            {
                lock (_extractionSyncRoot)
                lock (_renderingSyncRoot)
                lock (_verificationSyncRoot)
                {
                    var noMoreToBeExtractedHtmlDocuments = _memory.ToBeExtractedHtmlDocumentCount == 0;
                    var noMoreToBeRenderedUris = _memory.ToBeRenderedUriCount == 0;
                    var noMoreToBeVerifiedRawResources = _memory.ToBeVerifiedRawResourceCount == 0;
                    var nothingToDo = noMoreToBeExtractedHtmlDocuments && noMoreToBeRenderedUris && noMoreToBeVerifiedRawResources;
                    var noActiveThread = _pendingExtractionTaskCount + _pendingRenderingTaskCount + _pendingVerificationTaskCount == 0;
                    return nothingToDo && noActiveThread;
                }
            }
        }

        public int RemainingUrlCount => _pendingExtractionTaskCount + _pendingRenderingTaskCount + _pendingVerificationTaskCount +
                                        _memory.ToBeExtractedHtmlDocumentCount + _memory.ToBeRenderedUriCount +
                                        _memory.ToBeVerifiedRawResourceCount;

        public event Action<string> OnOrphanedResourcesDetected;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Management(IMemory memory)
        {
            _memory = memory;
            _cancellationTokenSource = new CancellationTokenSource();
            _rawResourceExtractorPool = new BlockingCollection<IRawResourceExtractor>();
            _rawResourceVerifierPool = new BlockingCollection<IRawResourceVerifier>();
            _webBrowserPool = new BlockingCollection<IWebBrowser>();
        }

        public void CancelEverything()
        {
            _cancellationTokenSource.Cancel();
            while (_pendingExtractionTaskCount + _pendingRenderingTaskCount + _pendingVerificationTaskCount > 0) Thread.Sleep(100);
            _memory.Clear();
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
            }
        }

        public void EnsureResources()
        {
            InitializeRawResourceExtractorPool();
            InitializeRawResourceVerifierPool();
            InitializeWebBrowserPool();

            void InitializeRawResourceExtractorPool()
            {
                for (var rawResourceExtractorId = 0; rawResourceExtractorId < RawResourceExtractorCount; rawResourceExtractorId++)
                {
                    CancellationToken.ThrowIfCancellationRequested();
                    var rawResourceExtractor = ServiceLocator.Get<IRawResourceExtractor>();
                    rawResourceExtractor.OnIdle += () => _rawResourceExtractorPool.Add(rawResourceExtractor, CancellationToken.None);
                    _rawResourceExtractorPool.Add(rawResourceExtractor, CancellationToken.None);
                }
            }
            void InitializeRawResourceVerifierPool()
            {
                for (var rawResourceVerifierId = 0; rawResourceVerifierId < RawResourceVerifierCount; rawResourceVerifierId++)
                {
                    CancellationToken.ThrowIfCancellationRequested();
                    var rawResourceVerifier = ServiceLocator.Get<IRawResourceVerifier>();
                    rawResourceVerifier.OnIdle += () => _rawResourceVerifierPool.Add(rawResourceVerifier, CancellationToken.None);
                    _rawResourceVerifierPool.Add(rawResourceVerifier, CancellationToken.None);
                }
            }
            void InitializeWebBrowserPool()
            {
                Parallel.For(0, _memory.Configurations.WebBrowserCount, webBrowserId =>
                {
                    CancellationToken.ThrowIfCancellationRequested();
                    var webBrowser = ServiceLocator.Get<IWebBrowser>();
                    webBrowser.OnIdle += () => _webBrowserPool.Add(webBrowser, CancellationToken.None);
                    webBrowser.OnRawResourceCaptured += rawResource => _memory.Memorize(rawResource, CancellationToken.None);
                    _webBrowserPool.Add(webBrowser, CancellationToken.None);
                });
            }
        }

        public void InterlockedCoordinate(out IRawResourceExtractor rawResourceExtractor, out HtmlDocument toBeExtractedHtmlDocument)
        {
            while (!EverythingIsDone && !CancellationToken.IsCancellationRequested)
            {
                Monitor.Enter(_extractionSyncRoot);
                if (_pendingExtractionTaskCount >= 300)
                {
                    Monitor.Exit(_extractionSyncRoot);
                    Thread.Sleep(100);
                    continue;
                }

                _pendingExtractionTaskCount++;
                if (!_memory.TryTakeToBeExtractedHtmlDocument(out toBeExtractedHtmlDocument))
                {
                    _pendingExtractionTaskCount--;
                    Monitor.Exit(_extractionSyncRoot);
                    Thread.Sleep(100);
                    continue;
                }
                Monitor.Exit(_extractionSyncRoot);

                try { rawResourceExtractor = _rawResourceExtractorPool.Take(CancellationToken); }
                catch (OperationCanceledException)
                {
                    _pendingExtractionTaskCount--;
                    throw;
                }
                return;
            }
            CancellationToken.ThrowIfCancellationRequested();
            throw new EverythingIsDoneException();
        }

        public void InterlockedCoordinate(out IWebBrowser webBrowser, out Uri toBeRenderedUri)
        {
            while (!EverythingIsDone && !CancellationToken.IsCancellationRequested)
            {
                Monitor.Enter(_renderingSyncRoot);
                if (_pendingRenderingTaskCount >= 300)
                {
                    Monitor.Exit(_renderingSyncRoot);
                    Thread.Sleep(100);
                    continue;
                }

                _pendingRenderingTaskCount++;
                if (!_memory.TryTakeToBeRenderedUri(out toBeRenderedUri))
                {
                    _pendingRenderingTaskCount--;
                    Monitor.Exit(_renderingSyncRoot);
                    Thread.Sleep(100);
                    continue;
                }
                Monitor.Exit(_renderingSyncRoot);

                try { webBrowser = _webBrowserPool.Take(CancellationToken); }
                catch (OperationCanceledException)
                {
                    _pendingRenderingTaskCount--;
                    throw;
                }
                return;
            }
            CancellationToken.ThrowIfCancellationRequested();
            throw new EverythingIsDoneException();
        }

        public void InterlockedCoordinate(out IRawResourceVerifier rawResourceVerifier, out RawResource toBeVerifiedRawResource)
        {
            while (!EverythingIsDone && !CancellationToken.IsCancellationRequested)
            {
                Monitor.Enter(_verificationSyncRoot);
                if (_pendingVerificationTaskCount >= 400)
                {
                    Monitor.Exit(_verificationSyncRoot);
                    Thread.Sleep(100);
                    continue;
                }

                _pendingVerificationTaskCount++;
                if (!_memory.TryTakeToBeVerifiedRawResource(out toBeVerifiedRawResource))
                {
                    _pendingVerificationTaskCount--;
                    Monitor.Exit(_verificationSyncRoot);
                    Thread.Sleep(100);
                    continue;
                }
                Monitor.Exit(_verificationSyncRoot);

                try { rawResourceVerifier = _rawResourceVerifierPool.Take(CancellationToken); }
                catch (OperationCanceledException)
                {
                    _pendingVerificationTaskCount--;
                    throw;
                }
                return;
            }
            CancellationToken.ThrowIfCancellationRequested();
            throw new EverythingIsDoneException();
        }

        public void OnRawResourceExtractionTaskCompleted(IRawResourceExtractor rawResourceExtractor = null,
            HtmlDocument toBeExtractedHtmlDocument = null)
        {
            lock (_extractionSyncRoot) _pendingExtractionTaskCount--;
            if (rawResourceExtractor != null) _rawResourceExtractorPool.Add(rawResourceExtractor, CancellationToken.None);
            if (toBeExtractedHtmlDocument != null) _memory.Memorize(toBeExtractedHtmlDocument, CancellationToken.None);
        }

        public void OnRawResourceVerificationTaskCompleted(IRawResourceVerifier rawResourceVerifier = null,
            RawResource toBeVerifiedRawResource = null)
        {
            lock (_verificationSyncRoot) _pendingVerificationTaskCount--;
            if (rawResourceVerifier != null) _rawResourceVerifierPool.Add(rawResourceVerifier, CancellationToken.None);
            if (toBeVerifiedRawResource != null) _memory.Memorize(toBeVerifiedRawResource, CancellationToken.None);
        }

        public void OnUriRenderingTaskCompleted(IWebBrowser webBrowser = null, Uri toBeRenderedUri = null)
        {
            lock (_renderingSyncRoot) _pendingRenderingTaskCount--;
            if (webBrowser != null) _webBrowserPool.Add(webBrowser, CancellationToken.None);
            if (toBeRenderedUri != null) _memory.Memorize(toBeRenderedUri, CancellationToken.None);
        }

        public bool TryTransitTo(CrawlerState crawlerState)
        {
            if (CrawlerState == CrawlerState.Unknown) return false;
            switch (crawlerState)
            {
                case CrawlerState.Ready:
                    lock (_syncRoot)
                    {
                        if (CrawlerState != CrawlerState.Stopping) return false;
                        CrawlerState = CrawlerState.Ready;
                        return true;
                    }
                case CrawlerState.Working:
                    lock (_syncRoot)
                    {
                        if (CrawlerState != CrawlerState.Ready && CrawlerState != CrawlerState.Paused) return false;
                        CrawlerState = CrawlerState.Working;
                        return true;
                    }
                case CrawlerState.Stopping:
                    lock (_syncRoot)
                    {
                        if (CrawlerState != CrawlerState.Working && CrawlerState != CrawlerState.Paused) return false;
                        CrawlerState = CrawlerState.Stopping;
                        return true;
                    }
                case CrawlerState.Paused:
                    lock (_syncRoot)
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

        void ReleaseUnmanagedResources()
        {
            var disposedRawResourceExtractorCount = 0;
            var disposedRawResourceVerifierCount = 0;
            var disposedWebBrowserCount = 0;
            DisposeRawResourceExtractorPool();
            DisposeRawResourceVerifierPool();
            DisposeWebBrowserPool();

            _cancellationTokenSource?.Dispose();
            _rawResourceExtractorPool?.Dispose();
            _rawResourceVerifierPool?.Dispose();
            _webBrowserPool?.Dispose();

            _cancellationTokenSource = null;
            _rawResourceExtractorPool = null;
            _rawResourceVerifierPool = null;
            _webBrowserPool = null;

            CheckForOrphanedResources();

            void DisposeRawResourceExtractorPool()
            {
                while (_rawResourceExtractorPool?.Any() ?? false)
                {
                    _rawResourceExtractorPool.Take();
                    disposedRawResourceExtractorCount++;
                }
            }
            void DisposeRawResourceVerifierPool()
            {
                while (_rawResourceVerifierPool?.Any() ?? false)
                {
                    _rawResourceVerifierPool.Take().Dispose();
                    disposedRawResourceVerifierCount++;
                }
            }
            void DisposeWebBrowserPool()
            {
                while (_webBrowserPool?.Any() ?? false)
                {
                    _webBrowserPool.Take().Dispose();
                    disposedWebBrowserCount++;
                }
            }
            void CheckForOrphanedResources()
            {
                var orphanedResourceErrorMessage = string.Empty;
                if (disposedRawResourceExtractorCount != RawResourceExtractorCount)
                    orphanedResourceErrorMessage += GetErrorMessage(
                        RawResourceExtractorCount,
                        nameof(RawResourceExtractor),
                        disposedRawResourceExtractorCount
                    );

                if (disposedRawResourceVerifierCount != RawResourceVerifierCount)
                    orphanedResourceErrorMessage += GetErrorMessage(
                        RawResourceVerifierCount,
                        nameof(RawResourceVerifier),
                        disposedRawResourceVerifierCount
                    );

                if (disposedWebBrowserCount != _memory.Configurations.WebBrowserCount)
                    orphanedResourceErrorMessage += GetErrorMessage(
                        _memory.Configurations.WebBrowserCount,
                        nameof(ChromiumWebBrowser),
                        disposedWebBrowserCount
                    );

                if (string.IsNullOrEmpty(orphanedResourceErrorMessage)) return;
                OnOrphanedResourcesDetected?.Invoke($"Orphaned resources detected!{orphanedResourceErrorMessage}");

                string GetErrorMessage(int createdCount, string resourceName, int disposedCount)
                {
                    resourceName = $"{resourceName}{(createdCount > 1 ? "s" : string.Empty)}";
                    var disposedCountText = disposedCount == 0 ? "none" : $"only {disposedCount}";
                    return $" There were {createdCount} {resourceName} created but {disposedCountText} could be found and disposed.";
                }
            }
        }

        public class EverythingIsDoneException : Exception { }

        ~Management() { ReleaseUnmanagedResources(); }
    }
}