using System;
using System.Collections.Generic;
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
        readonly object _extractionLock;
        readonly ILogger _logger;
        readonly IMemory _memory;
        bool _objectDisposed;
        int _pendingExtractionTaskCount;
        int _pendingRenderingTaskCount;
        int _pendingVerificationTaskCount;
        readonly Dictionary<string, object> _publicApiLockMap;
        readonly object _renderingLock;
        readonly IServicePool _servicePool;
        readonly object _verificationLock;

        public CancellationToken CancellationToken
        {
            get
            {
                lock (_publicApiLockMap[$"{nameof(CancellationToken)}Get"])
                {
                    if (_objectDisposed) throw new ObjectDisposedException(nameof(Scheduler));
                    return _cancellationTokenSource.Token;
                }
            }
        }

        public bool EverythingIsDone
        {
            get
            {
                lock (_publicApiLockMap[$"{nameof(EverythingIsDone)}Get"])
                {
                    if (_objectDisposed) throw new ObjectDisposedException(nameof(Scheduler));
                    lock (_extractionLock)
                    lock (_renderingLock)
                    lock (_verificationLock)
                    {
                        var noMoreToBeRenderedResources = _memory.ToBeRenderedResourceCount == 0;
                        var noMoreToBeVerifiedResources = _memory.ToBeVerifiedResourceCount == 0;
                        var noMoreToBeExtractedHtmlDocuments = _memory.ToBeExtractedHtmlDocumentCount == 0;
                        var nothingToDo = noMoreToBeExtractedHtmlDocuments && noMoreToBeRenderedResources && noMoreToBeVerifiedResources;
                        var noActiveThread = _pendingExtractionTaskCount + _pendingRenderingTaskCount + _pendingVerificationTaskCount == 0;
                        return nothingToDo && noActiveThread;
                    }
                }
            }
        }

        public int RemainingUrlCount
        {
            get
            {
                lock (_publicApiLockMap[$"{nameof(RemainingUrlCount)}Get"])
                {
                    if (_objectDisposed) throw new ObjectDisposedException(nameof(Scheduler));
                    lock (_extractionLock)
                    lock (_renderingLock)
                    lock (_verificationLock)
                    {
                        var toBeExtractedHtmlDocumentCount = _memory.ToBeExtractedHtmlDocumentCount;
                        var toBeRenderedResourceCount = _memory.ToBeRenderedResourceCount;
                        var toBeVerifiedResourceCount = _memory.ToBeVerifiedResourceCount;
                        return _pendingExtractionTaskCount + _pendingRenderingTaskCount + _pendingVerificationTaskCount +
                               toBeExtractedHtmlDocumentCount + toBeRenderedResourceCount + toBeVerifiedResourceCount;
                    }
                }
            }
        }

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Scheduler(IMemory memory, ILogger logger, IServicePool servicePool)
        {
            _memory = memory;
            _logger = logger;
            _servicePool = servicePool;
            _objectDisposed = false;
            _renderingLock = new object();
            _extractionLock = new object();
            _verificationLock = new object();
            _cancellationTokenSource = new CancellationTokenSource();
            _publicApiLockMap = new Dictionary<string, object>
            {
                { $"{nameof(CancellationToken)}Get", new object() },
                { $"{nameof(EverythingIsDone)}Get", new object() },
                { $"{nameof(RemainingUrlCount)}Get", new object() },
                { $"{nameof(CancelEverything)}", new object() },
                { "CreateExtractionTask", new object() },
                { "CreateRenderingTask", new object() },
                { "CreateVerificationTask", new object() }
            };
        }

        public void CancelEverything()
        {
            lock (_publicApiLockMap[$"{nameof(CancelEverything)}"])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(Scheduler));
                _cancellationTokenSource.Cancel();
                while (_pendingExtractionTaskCount + _pendingRenderingTaskCount + _pendingVerificationTaskCount > 0) Thread.Sleep(100);
                _memory.Clear();
            }
        }

        public void CreateTask(Action<IResourceExtractor, HtmlDocument> taskDescription)
        {
            lock (_publicApiLockMap["CreateExtractionTask"])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(Scheduler));
                IResourceExtractor resourceExtractor;
                HtmlDocument toBeExtractedHtmlDocument;
                try
                {
                    GetResourceExtractorAndToBeExtractedHtmlDocument();
                    Task.Run(
                        () =>
                        {
                            try { taskDescription(resourceExtractor, toBeExtractedHtmlDocument); }
                            catch (Exception exception) { _logger.LogException(exception); }
                            finally { ReleaseResourceExtractor(); }
                        },
                        CancellationToken
                    ).ContinueWith(
                        _ =>
                        {
                            ReleaseResourceExtractor();
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

                void GetResourceExtractorAndToBeExtractedHtmlDocument()
                {
                    while (!EverythingIsDone && !CancellationToken.IsCancellationRequested)
                    {
                        Monitor.Enter(_extractionLock);
                        if (_pendingExtractionTaskCount >= 300)
                        {
                            Monitor.Exit(_extractionLock);
                            Thread.Sleep(100);
                            continue;
                        }

                        _pendingExtractionTaskCount++;
                        if (!_memory.TryTakeToBeExtractedHtmlDocument(out toBeExtractedHtmlDocument))
                        {
                            _pendingExtractionTaskCount--;
                            Monitor.Exit(_extractionLock);
                            Thread.Sleep(100);
                            continue;
                        }
                        Monitor.Exit(_extractionLock);

                        try { resourceExtractor = _servicePool.GetResourceExtractor(CancellationToken); }
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
                void ReleaseResourceExtractor()
                {
                    lock (_extractionLock) _pendingExtractionTaskCount--;
                    _servicePool.Return(resourceExtractor);
                }
                void ReturnToBeExtractedHtmlDocument()
                {
                    _memory.MemorizeToBeExtractedHtmlDocument(toBeExtractedHtmlDocument, CancellationToken.None);
                }
            }
        }

        public void CreateTask(Action<IHtmlRenderer, Resource> taskDescription)
        {
            lock (_publicApiLockMap["CreateRenderingTask"])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(Scheduler));
                IHtmlRenderer htmlRenderer;
                Resource toBeRenderedResource;
                try
                {
                    GetHtmlRendererAndToBeRenderedResource();
                    (
                        (int) toBeRenderedResource.StatusCode >= 400
                            ? Task.Factory.StartNew(
                                ExecuteTaskDescription,
                                CancellationToken,
                                TaskCreationOptions.None,
                                PriorityTaskScheduler.Highest
                            )
                            : Task.Run(
                                ExecuteTaskDescription,
                                CancellationToken
                            )
                    ).ContinueWith(
                        _ =>
                        {
                            ReleaseHtmlRenderer();
                            ReturnToBeRenderedResource();
                        },
                        TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                    );
                }
                catch (Exception exception)
                {
                    if (exception is EverythingIsDoneException) return;
                    _logger.LogException(exception);
                }

                void ExecuteTaskDescription()
                {
                    try { taskDescription(htmlRenderer, toBeRenderedResource); }
                    catch (Exception exception) { _logger.LogException(exception); }
                    finally { ReleaseHtmlRenderer(); }
                }
                void GetHtmlRendererAndToBeRenderedResource()
                {
                    while (!EverythingIsDone && !CancellationToken.IsCancellationRequested)
                    {
                        Monitor.Enter(_renderingLock);
                        if (_pendingRenderingTaskCount >= 300)
                        {
                            Monitor.Exit(_renderingLock);
                            Thread.Sleep(100);
                            continue;
                        }

                        _pendingRenderingTaskCount++;
                        if (!_memory.TryTakeToBeRenderedResource(out toBeRenderedResource))
                        {
                            _pendingRenderingTaskCount--;
                            Monitor.Exit(_renderingLock);
                            Thread.Sleep(100);
                            continue;
                        }
                        Monitor.Exit(_renderingLock);

                        try { htmlRenderer = _servicePool.GetHtmlRenderer(CancellationToken); }
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
                void ReleaseHtmlRenderer()
                {
                    lock (_renderingLock) _pendingRenderingTaskCount--;
                    _servicePool.Return(htmlRenderer);
                }
                void ReturnToBeRenderedResource() { _memory.MemorizeToBeRenderedResource(toBeRenderedResource, CancellationToken.None); }
            }
        }

        public void CreateTask(Action<IResourceVerifier, Resource> taskDescription)
        {
            lock (_publicApiLockMap["CreateVerificationTask"])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(Scheduler));
                IResourceVerifier resourceVerifier;
                Resource toBeVerifiedResource;
                try
                {
                    GetResourceVerifierAndToBeVerifiedResource();
                    Task.Run(
                        () =>
                        {
                            try { taskDescription(resourceVerifier, toBeVerifiedResource); }
                            catch (Exception exception) { _logger.LogException(exception); }
                            finally { ReleaseResourceVerifier(); }
                        },
                        CancellationToken
                    ).ContinueWith(
                        _ =>
                        {
                            ReleaseResourceVerifier();
                            ReturnToBeVerifiedResource();
                        },
                        TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously
                    );
                }
                catch (Exception exception)
                {
                    if (exception is EverythingIsDoneException) return;
                    _logger.LogException(exception);
                }

                void GetResourceVerifierAndToBeVerifiedResource()
                {
                    while (!EverythingIsDone && !CancellationToken.IsCancellationRequested)
                    {
                        Monitor.Enter(_verificationLock);
                        if (_pendingVerificationTaskCount >= 400)
                        {
                            Monitor.Exit(_verificationLock);
                            Thread.Sleep(100);
                            continue;
                        }

                        _pendingVerificationTaskCount++;
                        if (!_memory.TryTakeToBeVerifiedResource(out toBeVerifiedResource))
                        {
                            _pendingVerificationTaskCount--;
                            Monitor.Exit(_verificationLock);
                            Thread.Sleep(100);
                            continue;
                        }
                        Monitor.Exit(_verificationLock);

                        try { resourceVerifier = _servicePool.GetResourceVerifier(CancellationToken); }
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
                void ReleaseResourceVerifier()
                {
                    lock (_verificationLock) _pendingVerificationTaskCount--;
                    _servicePool.Return(resourceVerifier);
                }
                // TODO: Not gonna work because of _alreadyVerifiedUrls in Memory.cs
                void ReturnToBeVerifiedResource() { _memory.MemorizeToBeVerifiedResource(toBeVerifiedResource, CancellationToken.None); }
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

        void ReleaseUnmanagedResources()
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        class EverythingIsDoneException : Exception { }

        ~Scheduler() { ReleaseUnmanagedResources(); }
    }
}