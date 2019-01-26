using System;
using System.Threading;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class Management : IManagement
    {
        int _activeExtractionThreadCount;
        int _activeRenderingThreadCount;
        int _activeVerificationThreadCount;
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly IMemory _memory;
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
                    var noActiveThread = _activeExtractionThreadCount + _activeRenderingThreadCount + _activeVerificationThreadCount == 0;
                    return nothingToDo && noActiveThread;
                }
            }
        }

        public int RemainingUrlCount => _activeVerificationThreadCount + _memory.ToBeVerifiedRawResourceCount;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Management(IMemory memory)
        {
            _memory = memory;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void CancelEverything() { _cancellationTokenSource.Cancel(); }

        public void InterlockedDecrementActiveExtractionThreadCount()
        {
            lock (ExtractionSyncRoot) _activeExtractionThreadCount--;
        }

        public void InterlockedDecrementActiveRenderingThreadCount()
        {
            lock (RenderingSyncRoot) _activeRenderingThreadCount--;
        }

        public void InterlockedDecrementActiveVerificationThreadCount()
        {
            lock (VerificationSyncRoot) _activeVerificationThreadCount--;
        }

        public void InterlockedIncrementActiveExtractionThreadCount()
        {
            InterlockedIncrement(ref _activeExtractionThreadCount, ExtractionSyncRoot, 300);
        }

        public void InterlockedIncrementActiveRenderingThreadCount()
        {
            InterlockedIncrement(ref _activeRenderingThreadCount, RenderingSyncRoot, 300);
        }

        public void InterlockedIncrementActiveVerificationThreadCount()
        {
            InterlockedIncrement(ref _activeVerificationThreadCount, VerificationSyncRoot, 400);
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
            while (!EverythingIsDone)
            {
                Monitor.Enter(syncRoot);
                if (value >= boundary)
                {
                    Monitor.Exit(syncRoot);
                    Thread.Sleep(TimeSpan.FromSeconds(3));
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