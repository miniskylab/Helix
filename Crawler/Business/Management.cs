using System;
using System.Threading;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class Management : IManagement
    {
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly IMemory _memory;
        int _pendingExtractionTaskCount;
        int _pendingRenderingTaskCount;
        int _pendingVerificationTaskCount;
        static readonly object ExtractionSyncRoot = new object();
        static readonly object RenderingSyncRoot = new object();
        static readonly object SyncRoot = new object();
        static readonly object VerificationSyncRoot = new object();

        public CrawlerState CrawlerState { get; private set; } = CrawlerState.Ready;

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public bool EverythingIsDone
        {
            get
            {
                lock (ExtractionSyncRoot)
                lock (RenderingSyncRoot)
                lock (VerificationSyncRoot)
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

        public int RemainingUrlCount => _pendingVerificationTaskCount + _memory.ToBeVerifiedRawResourceCount;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Management(IMemory memory)
        {
            _memory = memory;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void CancelEverything() { _cancellationTokenSource.Cancel(); }

        public HtmlDocument InterlockedTakeToBeExtractedHtmlDocument()
        {
            while (!EverythingIsDone && !CancellationToken.IsCancellationRequested)
            {
                Monitor.Enter(ExtractionSyncRoot);
                if (!_memory.TryTakeToBeExtractedHtmlDocument(out var toBeExtractedHtmlDocument))
                {
                    Monitor.Exit(ExtractionSyncRoot);
                    Thread.Sleep(100);
                    continue;
                }
                InterlockedIncrement(ref _pendingExtractionTaskCount, ExtractionSyncRoot, 300);
                Monitor.Exit(ExtractionSyncRoot);
                return toBeExtractedHtmlDocument;
            }
            throw new OperationCanceledException(CancellationToken);
        }

        public Uri InterlockedTakeToBeRenderedUri()
        {
            while (!EverythingIsDone && !CancellationToken.IsCancellationRequested)
            {
                Monitor.Enter(RenderingSyncRoot);
                if (!_memory.TryTakeToBeRenderedUri(out var toBeRenderedUri))
                {
                    Monitor.Exit(RenderingSyncRoot);
                    Thread.Sleep(100);
                    continue;
                }
                InterlockedIncrement(ref _pendingRenderingTaskCount, RenderingSyncRoot, 300);
                Monitor.Exit(RenderingSyncRoot);
                return toBeRenderedUri;
            }
            throw new OperationCanceledException(CancellationToken);
        }

        public RawResource InterlockedTakeToBeVerifiedRawResource()
        {
            while (!EverythingIsDone && !CancellationToken.IsCancellationRequested)
            {
                Monitor.Enter(VerificationSyncRoot);
                if (!_memory.TryTakeToBeVerifiedRawResource(out var toBeVerifiedRawResource))
                {
                    Monitor.Exit(VerificationSyncRoot);
                    Thread.Sleep(100);
                    continue;
                }
                InterlockedIncrement(ref _pendingVerificationTaskCount, VerificationSyncRoot, 400); // TODO: bug :v
                Monitor.Exit(VerificationSyncRoot);
                return toBeVerifiedRawResource;
            }
            throw new OperationCanceledException(CancellationToken);
        }

        public void OnRawResourceExtractionTaskCompleted()
        {
            lock (ExtractionSyncRoot) _pendingExtractionTaskCount--;
        }

        public void OnRawResourceVerificationTaskCompleted()
        {
            lock (VerificationSyncRoot) _pendingVerificationTaskCount--;
        }

        public void OnUriRenderingTaskCompleted()
        {
            lock (RenderingSyncRoot) _pendingRenderingTaskCount--;
        }

        public bool TryTransitTo(CrawlerState crawlerState)
        {
            if (CrawlerState == CrawlerState.Unknown) return false;
            switch (crawlerState)
            {
                case CrawlerState.Ready:
                    lock (SyncRoot)
                    {
                        if (CrawlerState != CrawlerState.Stopping) return false;
                        CrawlerState = CrawlerState.Ready;
                        return true;
                    }
                case CrawlerState.Working:
                    lock (SyncRoot)
                    {
                        if (CrawlerState != CrawlerState.Ready && CrawlerState != CrawlerState.Paused) return false;
                        CrawlerState = CrawlerState.Working;
                        return true;
                    }
                case CrawlerState.Stopping:
                    lock (SyncRoot)
                    {
                        if (CrawlerState != CrawlerState.Working && CrawlerState != CrawlerState.Paused) return false;
                        CrawlerState = CrawlerState.Stopping;
                        return true;
                    }
                case CrawlerState.Paused:
                    lock (SyncRoot)
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

        void InterlockedIncrement(ref int value, object syncRoot, int boundary)
        {
            while (!CancellationToken.IsCancellationRequested)
            {
                Monitor.Enter(syncRoot);
                if (value >= boundary)
                {
                    Monitor.Exit(syncRoot);
                    Thread.Sleep(100);
                    continue;
                }
                value++;
                Monitor.Exit(syncRoot);
                break;
            }
        }

        ~Management() { _cancellationTokenSource?.Dispose(); }
    }
}