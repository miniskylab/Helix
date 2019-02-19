using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Crawler
{
    public class ServicePool : IServicePool
    {
        const int RawResourceExtractorCount = 300;
        const int RawResourceVerifierCount = 2500;
        BlockingCollection<IHtmlRenderer> _htmlRendererPool;
        readonly ILogger _logger;
        readonly IMemory _memory;
        bool _objectDisposed;
        readonly Dictionary<string, object> _publicApiLockMap;
        BlockingCollection<IRawResourceExtractor> _rawResourceExtractorPool;
        BlockingCollection<IRawResourceVerifier> _rawResourceVerifierPool;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ServicePool(IMemory memory, ILogger logger)
        {
            _memory = memory;
            _logger = logger;
            _objectDisposed = false;
            _rawResourceExtractorPool = new BlockingCollection<IRawResourceExtractor>();
            _rawResourceVerifierPool = new BlockingCollection<IRawResourceVerifier>();
            _htmlRendererPool = new BlockingCollection<IHtmlRenderer>();
            _publicApiLockMap = new Dictionary<string, object>
            {
                { $"{nameof(EnsureEnoughResources)}", new object() },
                { $"{nameof(GetHtmlRenderer)}", new object() },
                { $"{nameof(GetRawResourceExtractor)}", new object() },
                { $"{nameof(GetResourceVerifier)}", new object() },
                { $"{nameof(Return)}{nameof(RawResourceExtractor)}", new object() },
                { $"{nameof(Return)}{nameof(RawResourceVerifier)}", new object() },
                { $"{nameof(Return)}{nameof(HtmlRenderer)}", new object() }
            };
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

        public void EnsureEnoughResources(CancellationToken cancellationToken)
        {
            lock (_publicApiLockMap[nameof(EnsureEnoughResources)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
                InitializeRawResourceExtractorPool();
                InitializeRawResourceVerifierPool();
                InitializeHtmlRendererPool();

                void InitializeRawResourceExtractorPool()
                {
                    for (var rawResourceExtractorId = 0; rawResourceExtractorId < RawResourceExtractorCount; rawResourceExtractorId++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var rawResourceExtractor = ServiceLocator.Get<IRawResourceExtractor>();
                        _rawResourceExtractorPool.Add(rawResourceExtractor, cancellationToken);
                    }
                }
                void InitializeRawResourceVerifierPool()
                {
                    for (var rawResourceVerifierId = 0; rawResourceVerifierId < RawResourceVerifierCount; rawResourceVerifierId++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var rawResourceVerifier = ServiceLocator.Get<IRawResourceVerifier>();
                        _rawResourceVerifierPool.Add(rawResourceVerifier, cancellationToken);
                    }
                }
                void InitializeHtmlRendererPool()
                {
                    Parallel.For(0, _memory.Configurations.HtmlRendererCount, htmlRendererId =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var htmlRenderer = ServiceLocator.Get<IHtmlRenderer>();
                        htmlRenderer.OnRawResourceCaptured += rawResource => _memory.Memorize(rawResource, CancellationToken.None);
                        _htmlRendererPool.Add(htmlRenderer, cancellationToken);
                    });
                }
            }
        }

        public IHtmlRenderer GetHtmlRenderer(CancellationToken cancellationToken)
        {
            lock (_publicApiLockMap[nameof(GetHtmlRenderer)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
                return _htmlRendererPool.Take(cancellationToken);
            }
        }

        public IRawResourceExtractor GetRawResourceExtractor(CancellationToken cancellationToken)
        {
            lock (_publicApiLockMap[nameof(GetRawResourceExtractor)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
                return _rawResourceExtractorPool.Take(cancellationToken);
            }
        }

        public IRawResourceVerifier GetResourceVerifier(CancellationToken cancellationToken)
        {
            lock (_publicApiLockMap[nameof(GetResourceVerifier)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
                return _rawResourceVerifierPool.Take(cancellationToken);
            }
        }

        public void Return(IRawResourceExtractor rawResourceExtractor)
        {
            lock (_publicApiLockMap[$"{nameof(Return)}{nameof(RawResourceExtractor)}"])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
                _rawResourceExtractorPool.Add(rawResourceExtractor);
            }
        }

        public void Return(IRawResourceVerifier rawResourceVerifier)
        {
            lock (_publicApiLockMap[$"{nameof(Return)}{nameof(RawResourceVerifier)}"])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
                _rawResourceVerifierPool.Add(rawResourceVerifier);
            }
        }

        public void Return(IHtmlRenderer htmlRenderer)
        {
            lock (_publicApiLockMap[$"{nameof(Return)}{nameof(HtmlRenderer)}"])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
                _htmlRendererPool.Add(htmlRenderer);
            }
        }

        void ReleaseUnmanagedResources()
        {
            var disposedRawResourceExtractorCount = 0;
            var disposedRawResourceVerifierCount = 0;
            var disposedHtmlRendererCount = 0;
            DisposeRawResourceExtractorPool();
            DisposeRawResourceVerifierPool();
            DisposeHtmlRendererPool();

            _rawResourceExtractorPool?.Dispose();
            _rawResourceVerifierPool?.Dispose();
            _htmlRendererPool?.Dispose();

            _rawResourceExtractorPool = null;
            _rawResourceVerifierPool = null;
            _htmlRendererPool = null;

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
            void DisposeHtmlRendererPool()
            {
                while (_htmlRendererPool?.Any() ?? false)
                {
                    _htmlRendererPool.Take().Dispose();
                    disposedHtmlRendererCount++;
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

                if (disposedHtmlRendererCount != _memory.Configurations.HtmlRendererCount)
                    orphanedResourceErrorMessage += GetErrorMessage(
                        _memory.Configurations.HtmlRendererCount,
                        nameof(HtmlRenderer),
                        disposedHtmlRendererCount
                    );

                if (string.IsNullOrEmpty(orphanedResourceErrorMessage)) return;
                _logger.LogInfo($"Orphaned resources detected!{orphanedResourceErrorMessage}");

                string GetErrorMessage(int createdCount, string resourceName, int disposedCount)
                {
                    resourceName = $"{resourceName}{(createdCount > 1 ? "s" : string.Empty)}";
                    var disposedCountText = disposedCount == 0 ? "none" : $"only {disposedCount}";
                    return $"\r\nThere were {createdCount} {resourceName} created but {disposedCountText} could be found and disposed.";
                }
            }
        }

        ~ServicePool() { ReleaseUnmanagedResources(); }
    }
}