using System;
using System.Threading;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Crawler
{
    public sealed class Scheduler : IScheduler
    {
        CancellationTokenSource _cancellationTokenSource;
        readonly object _disposalSyncRoot;
        readonly object _extractionSyncRoot;
        readonly ILogger _logger;
        readonly IMemory _memory;
        int _pendingExtractionTaskCount;
        int _pendingRenderingTaskCount;
        int _pendingVerificationTaskCount;
        readonly object _renderingSyncRoot;
        readonly IServicePool _servicePool;
        readonly object _verificationSyncRoot;

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

        public int RemainingUrlCount
        {
            get
            {
                lock (_extractionSyncRoot)
                lock (_renderingSyncRoot)
                lock (_verificationSyncRoot)
                {
                    return _pendingExtractionTaskCount + _pendingRenderingTaskCount + _pendingVerificationTaskCount +
                           _memory.ToBeExtractedHtmlDocumentCount + _memory.ToBeRenderedUriCount + _memory.ToBeVerifiedRawResourceCount;
                }
            }
        }

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Scheduler(IMemory memory, ILogger logger, IServicePool servicePool)
        {
            _memory = memory;
            _logger = logger;
            _servicePool = servicePool;
            _disposalSyncRoot = new object();
            _renderingSyncRoot = new object();
            _extractionSyncRoot = new object();
            _verificationSyncRoot = new object();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void CancelEverything()
        {
            _cancellationTokenSource.Cancel();
            while (_pendingExtractionTaskCount + _pendingRenderingTaskCount + _pendingVerificationTaskCount > 0) Thread.Sleep(100);
            _memory.Clear();
        }

        public void CreateTask(Action<IRawResourceExtractor, HtmlDocument> taskDescription)
        {
            IRawResourceExtractor rawResourceExtractor;
            HtmlDocument toBeExtractedHtmlDocument;
            try
            {
                GetRawResourceExtractorAndToBeExtractedHtmlDocument();
                Task.Run(
                    () =>
                    {
                        try { taskDescription(rawResourceExtractor, toBeExtractedHtmlDocument); }
                        catch (OperationCanceledException operationCanceledException) { _logger.LogException(operationCanceledException); }
                        finally { ReleaseRawResourceExtractor(); }
                    },
                    CancellationToken
                ).ContinueWith(
                    _ =>
                    {
                        ReleaseRawResourceExtractor();
                        ReturnToBeExtractedHtmlDocument();
                    },
                    TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                );
            }
            catch (Exception exception)
            {
                if (exception is EverythingIsDoneException) return;
                _logger.LogException(exception);
            }

            void GetRawResourceExtractorAndToBeExtractedHtmlDocument()
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
                    if (!_memory.TryTake(out toBeExtractedHtmlDocument))
                    {
                        _pendingExtractionTaskCount--;
                        Monitor.Exit(_extractionSyncRoot);
                        Thread.Sleep(100);
                        continue;
                    }
                    Monitor.Exit(_extractionSyncRoot);

                    try { rawResourceExtractor = _servicePool.GetRawResourceExtractor(CancellationToken); }
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
            void ReleaseRawResourceExtractor()
            {
                lock (_extractionSyncRoot) _pendingExtractionTaskCount--;
                _servicePool.Return(rawResourceExtractor);
            }
            void ReturnToBeExtractedHtmlDocument() { _memory.Memorize(toBeExtractedHtmlDocument, CancellationToken.None); }
        }

        public void CreateTask(Action<IWebBrowser, Uri> taskDescription)
        {
            IWebBrowser webBrowser;
            Uri toBeRenderedUri;
            try
            {
                GetWebBrowserAndToBeRenderedUri();
                Task.Run(
                    () =>
                    {
                        try { taskDescription(webBrowser, toBeRenderedUri); }
                        catch (OperationCanceledException operationCanceledException) { _logger.LogException(operationCanceledException); }
                        finally { ReleaseWebBrowser(); }
                    },
                    CancellationToken
                ).ContinueWith(
                    _ =>
                    {
                        ReleaseWebBrowser();
                        ReturnToBeRenderedUri();
                    },
                    TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                );
            }
            catch (Exception exception)
            {
                if (exception is EverythingIsDoneException) return;
                _logger.LogException(exception);
            }

            void GetWebBrowserAndToBeRenderedUri()
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
                    if (!_memory.TryTake(out toBeRenderedUri))
                    {
                        _pendingRenderingTaskCount--;
                        Monitor.Exit(_renderingSyncRoot);
                        Thread.Sleep(100);
                        continue;
                    }
                    Monitor.Exit(_renderingSyncRoot);

                    try { webBrowser = _servicePool.GetWebBrowser(CancellationToken); }
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
            void ReleaseWebBrowser()
            {
                lock (_renderingSyncRoot) _pendingRenderingTaskCount--;
                _servicePool.Return(webBrowser);
            }
            void ReturnToBeRenderedUri() { _memory.Memorize(toBeRenderedUri, CancellationToken.None); }
        }

        public void CreateTask(Action<IRawResourceVerifier, RawResource> taskDescription)
        {
            IRawResourceVerifier rawResourceVerifier;
            RawResource toBeVerifiedRawResource;
            try
            {
                GetRawResourceVerifierAndToBeVerifiedRawResource();
                Task.Run(
                    () =>
                    {
                        try { taskDescription(rawResourceVerifier, toBeVerifiedRawResource); }
                        catch (OperationCanceledException operationCanceledException) { _logger.LogException(operationCanceledException); }
                        finally { ReleaseRawResourceVerifier(); }
                    },
                    CancellationToken
                ).ContinueWith(
                    _ =>
                    {
                        ReleaseRawResourceVerifier();
                        ReturnToBeVerifiedRawResource();
                    },
                    TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                );
            }
            catch (Exception exception)
            {
                if (exception is EverythingIsDoneException) return;
                _logger.LogException(exception);
            }

            void GetRawResourceVerifierAndToBeVerifiedRawResource()
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
                    if (!_memory.TryTake(out toBeVerifiedRawResource))
                    {
                        _pendingVerificationTaskCount--;
                        Monitor.Exit(_verificationSyncRoot);
                        Thread.Sleep(100);
                        continue;
                    }
                    Monitor.Exit(_verificationSyncRoot);

                    try { rawResourceVerifier = _servicePool.GetResourceVerifier(CancellationToken); }
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
            void ReleaseRawResourceVerifier()
            {
                lock (_verificationSyncRoot) _pendingVerificationTaskCount--;
                _servicePool.Return(rawResourceVerifier);
            }
            void ReturnToBeVerifiedRawResource() { _memory.Memorize(toBeVerifiedRawResource, CancellationToken.None); }
        }

        public void Dispose()
        {
            lock (_disposalSyncRoot)
            {
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
            }
        }

        void ReleaseUnmanagedResources()
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        class EverythingIsDoneException : Exception { }

        ~Scheduler() { ReleaseUnmanagedResources(); }
    }
}