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
        const int ResourceExtractorCount = 300;
        const int ResourceVerifierCount = 2500;
        int _createdHtmlRendererCount;
        int _createdResourceExtractorCount;
        int _createdResourceVerifierCount;
        BlockingCollection<IHtmlRenderer> _htmlRendererPool;
        readonly ILogger _logger;
        readonly IMemory _memory;
        bool _objectDisposed;
        readonly Dictionary<string, object> _publicApiLockMap;
        BlockingCollection<IResourceExtractor> _resourceExtractorPool;
        BlockingCollection<IResourceVerifier> _resourceVerifierPool;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ServicePool(IMemory memory, ILogger logger)
        {
            _memory = memory;
            _logger = logger;
            _objectDisposed = false;
            _resourceExtractorPool = new BlockingCollection<IResourceExtractor>();
            _resourceVerifierPool = new BlockingCollection<IResourceVerifier>();
            _htmlRendererPool = new BlockingCollection<IHtmlRenderer>();
            _publicApiLockMap = new Dictionary<string, object>
            {
                { $"{nameof(EnsureEnoughResources)}", new object() },
                { $"{nameof(GetHtmlRenderer)}", new object() },
                { $"{nameof(GetResourceExtractor)}", new object() },
                { $"{nameof(GetResourceVerifier)}", new object() },
                { $"{nameof(Return)}{nameof(ResourceExtractor)}", new object() },
                { $"{nameof(Return)}{nameof(ResourceVerifier)}", new object() },
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
                InitializeResourceExtractorPool();
                InitializeResourceVerifierPool();
                InitializeHtmlRendererPool();

                void InitializeResourceExtractorPool()
                {
                    for (var resourceExtractorId = 0; resourceExtractorId < ResourceExtractorCount; resourceExtractorId++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            while (_resourceExtractorPool.Any()) _resourceExtractorPool.Take();
                            _createdResourceExtractorCount = 0;
                        }
                        else
                        {
                            var resourceExtractor = ServiceLocator.Get<IResourceExtractor>();
                            _resourceExtractorPool.Add(resourceExtractor, CancellationToken.None);
                            _createdResourceExtractorCount++;
                        }
                    }
                }
                void InitializeResourceVerifierPool()
                {
                    for (var resourceVerifierId = 0; resourceVerifierId < ResourceVerifierCount; resourceVerifierId++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            while (_resourceVerifierPool.Any()) _resourceVerifierPool.Take().Dispose();
                            _createdResourceVerifierCount = 0;
                        }
                        else
                        {
                            var resourceVerifier = ServiceLocator.Get<IResourceVerifier>();
                            _resourceVerifierPool.Add(resourceVerifier, CancellationToken.None);
                            _createdResourceVerifierCount++;
                        }
                    }
                }
                void InitializeHtmlRendererPool()
                {
                    Parallel.For(0, _memory.Configurations.HtmlRendererCount, htmlRendererId =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            while (_htmlRendererPool.Any()) _htmlRendererPool.Take().Dispose();
                            Interlocked.Exchange(ref _createdHtmlRendererCount, 0);
                        }
                        else
                        {
                            var htmlRenderer = ServiceLocator.Get<IHtmlRenderer>();
                            htmlRenderer.OnResourceCaptured += resource =>
                            {
                                _memory.MemorizeToBeVerifiedResource(resource, cancellationToken);
                            };
                            _htmlRendererPool.Add(htmlRenderer, CancellationToken.None);
                            Interlocked.Increment(ref _createdHtmlRendererCount);
                        }
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

        public IResourceExtractor GetResourceExtractor(CancellationToken cancellationToken)
        {
            lock (_publicApiLockMap[nameof(GetResourceExtractor)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
                return _resourceExtractorPool.Take(cancellationToken);
            }
        }

        public IResourceVerifier GetResourceVerifier(CancellationToken cancellationToken)
        {
            lock (_publicApiLockMap[nameof(GetResourceVerifier)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
                return _resourceVerifierPool.Take(cancellationToken);
            }
        }

        public void Return(IResourceExtractor resourceExtractor)
        {
            lock (_publicApiLockMap[$"{nameof(Return)}{nameof(ResourceExtractor)}"])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
                _resourceExtractorPool.Add(resourceExtractor);
            }
        }

        public void Return(IResourceVerifier resourceVerifier)
        {
            lock (_publicApiLockMap[$"{nameof(Return)}{nameof(ResourceVerifier)}"])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
                _resourceVerifierPool.Add(resourceVerifier);
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
            var disposedResourceExtractorCount = 0;
            var disposedResourceVerifierCount = 0;
            var disposedHtmlRendererCount = 0;
            DisposeResourceExtractorPool();
            DisposeResourceVerifierPool();
            DisposeHtmlRendererPool();

            _resourceExtractorPool?.Dispose();
            _resourceVerifierPool?.Dispose();
            _htmlRendererPool?.Dispose();

            _resourceExtractorPool = null;
            _resourceVerifierPool = null;
            _htmlRendererPool = null;

            CheckForOrphanedResources();

            void DisposeResourceExtractorPool()
            {
                while (_resourceExtractorPool?.Any() ?? false)
                {
                    _resourceExtractorPool.Take();
                    disposedResourceExtractorCount++;
                }
            }
            void DisposeResourceVerifierPool()
            {
                while (_resourceVerifierPool?.Any() ?? false)
                {
                    _resourceVerifierPool.Take().Dispose();
                    disposedResourceVerifierCount++;
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
                if (disposedResourceExtractorCount != _createdResourceExtractorCount)
                    orphanedResourceErrorMessage += GetErrorMessage(
                        ResourceExtractorCount,
                        nameof(ResourceExtractor),
                        disposedResourceExtractorCount
                    );

                if (disposedResourceVerifierCount != _createdResourceVerifierCount)
                    orphanedResourceErrorMessage += GetErrorMessage(
                        ResourceVerifierCount,
                        nameof(ResourceVerifier),
                        disposedResourceVerifierCount
                    );

                if (disposedHtmlRendererCount != _createdHtmlRendererCount)
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