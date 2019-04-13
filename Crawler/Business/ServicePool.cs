using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Crawler
{
    public class ServicePool : IServicePool
    {
        readonly Configurations _configurations;
        int _createdHtmlRendererCount;
        int _createdResourceExtractorCount;
        int _createdResourceVerifierCount;
        readonly IEventBroadcaster _eventBroadcaster;
        readonly IHardwareMonitor _hardwareMonitor;
        BlockingCollection<IHtmlRenderer> _htmlRendererPool;
        readonly ILogger _logger;
        readonly IMemory _memory;
        bool _objectDisposed;
        BlockingCollection<IResourceExtractor> _resourceExtractorPool;
        BlockingCollection<IResourceVerifier> _resourceVerifierPool;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ServicePool(Configurations configurations, ILogger logger, IEventBroadcaster eventBroadcaster, IMemory memory,
            IHardwareMonitor hardwareMonitor)
        {
            _memory = memory;
            _logger = logger;
            _configurations = configurations;
            _hardwareMonitor = hardwareMonitor;
            _eventBroadcaster = eventBroadcaster;
            _objectDisposed = false;
            _resourceExtractorPool = new BlockingCollection<IResourceExtractor>();
            _resourceVerifierPool = new BlockingCollection<IResourceVerifier>();
            _htmlRendererPool = new BlockingCollection<IHtmlRenderer>();
        }

        public void Dispose()
        {
            if (_objectDisposed) return;
            _hardwareMonitor.StopMonitoring();
            _hardwareMonitor.Dispose();
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
            _objectDisposed = true;
        }

        public IHtmlRenderer GetHtmlRenderer(CancellationToken cancellationToken)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
            return _htmlRendererPool.Take(cancellationToken);
        }

        public IResourceExtractor GetResourceExtractor(CancellationToken cancellationToken)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
            return _resourceExtractorPool.Take(cancellationToken);
        }

        public IResourceVerifier GetResourceVerifier(CancellationToken cancellationToken)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
            return _resourceVerifierPool.Take(cancellationToken);
        }

        public void PreCreateServices(CancellationToken cancellationToken)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
            PreCreateResourceExtractors();
            PreCreateResourceVerifiers();
            CreateHtmlRenderersAdaptively();

            void PreCreateResourceExtractors()
            {
                for (var resourceExtractorId = 0; resourceExtractorId < _configurations.ResourceExtractorCount; resourceExtractorId++)
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
            void PreCreateResourceVerifiers()
            {
                for (var resourceVerifierId = 0; resourceVerifierId < _configurations.ResourceVerifierCount; resourceVerifierId++)
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
            void CreateHtmlRenderersAdaptively()
            {
                CreateHtmlRenderer();
                _hardwareMonitor.OnLowCpuUsage += averageCpuUtilization =>
                {
                    if (_htmlRendererPool.Count > 0 || _createdHtmlRendererCount == _configurations.MaxHtmlRendererCount) return;
                    if (cancellationToken.IsCancellationRequested) return;

                    CreateHtmlRenderer();
                    _logger.LogInfo(
                        $"Low CPU usage detected ({Math.Round(100 * averageCpuUtilization, 0)}%). " +
                        $"Browser count increased from {_createdHtmlRendererCount - 1} to {_createdHtmlRendererCount}."
                    );
                };
                _hardwareMonitor.OnHighCpuUsage += averageCpuUtilization =>
                {
                    if (_createdHtmlRendererCount == 1) return;
                    if (cancellationToken.IsCancellationRequested) return;

                    _htmlRendererPool.Take().Dispose();
                    _createdHtmlRendererCount--;
                    _logger.LogInfo(
                        $"High CPU usage detected ({Math.Round(100 * averageCpuUtilization, 0)}%). " +
                        $"Browser count decreased from {_createdHtmlRendererCount + 1} to {_createdHtmlRendererCount}."
                    );
                };
                _hardwareMonitor.StartMonitoring();

                void CreateHtmlRenderer()
                {
                    var htmlRenderer = ServiceLocator.Get<IHtmlRenderer>();
                    htmlRenderer.OnResourceCaptured += resource =>
                    {
                        _memory.MemorizeToBeVerifiedResource(resource, cancellationToken);
                    };
                    _htmlRendererPool.Add(htmlRenderer, CancellationToken.None);
                    _createdHtmlRendererCount++;
                }
            }
        }

        public void Return(IResourceExtractor resourceExtractor)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
            _resourceExtractorPool.Add(resourceExtractor);
        }

        public void Return(IResourceVerifier resourceVerifier)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
            _resourceVerifierPool.Add(resourceVerifier);
        }

        public void Return(IHtmlRenderer htmlRenderer)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ServicePool));
            _htmlRendererPool.Add(htmlRenderer);
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

                    _eventBroadcaster.Broadcast(new Event
                    {
                        EventType = EventType.StopProgressUpdated,
                        Message = $"Closing web browsers ({disposedHtmlRendererCount}/{_createdHtmlRendererCount})"
                    });
                }
            }
            void CheckForOrphanedResources()
            {
                var orphanedResourceErrorMessage = string.Empty;
                if (disposedResourceExtractorCount != _createdResourceExtractorCount)
                    orphanedResourceErrorMessage += GetErrorMessage(
                        _createdResourceExtractorCount,
                        nameof(ResourceExtractor),
                        disposedResourceExtractorCount
                    );

                if (disposedResourceVerifierCount != _createdResourceVerifierCount)
                    orphanedResourceErrorMessage += GetErrorMessage(
                        _createdResourceVerifierCount,
                        nameof(ResourceVerifier),
                        disposedResourceVerifierCount
                    );

                if (disposedHtmlRendererCount != _createdHtmlRendererCount)
                    orphanedResourceErrorMessage += GetErrorMessage(
                        _createdHtmlRendererCount,
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