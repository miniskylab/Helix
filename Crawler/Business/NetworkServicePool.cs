using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Crawler
{
    public class NetworkServicePool : INetworkServicePool
    {
        readonly IEventBroadcaster _eventBroadcaster;
        BlockingCollection<IHtmlRenderer> _htmlRendererPool;
        readonly ILogger _logger;
        bool _objectDisposed;
        BlockingCollection<IResourceExtractor> _resourceExtractorPool;
        BlockingCollection<IResourceVerifier> _resourceVerifierPool;
        Statistics _statistics;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public NetworkServicePool(Configurations configurations, ILogger logger, IEventBroadcaster eventBroadcaster, IMemory memory,
            IHardwareMonitor hardwareMonitor)
        {
            _logger = logger;
            _objectDisposed = false;
            _statistics = new Statistics();
            _eventBroadcaster = eventBroadcaster;
            _resourceExtractorPool = new BlockingCollection<IResourceExtractor>();
            _resourceVerifierPool = new BlockingCollection<IResourceVerifier>();
            _htmlRendererPool = new BlockingCollection<IHtmlRenderer>();

            PreCreateServices();
            CreateAndDestroyHtmlRenderersAdaptively();

            void PreCreateServices()
            {
                PreCreateResourceExtractors();
                PreCreateResourceVerifiers();
                CreateHtmlRenderer();

                void PreCreateResourceExtractors()
                {
                    for (var resourceExtractorId = 0; resourceExtractorId < configurations.ResourceExtractorCount; resourceExtractorId++)
                    {
                        var resourceExtractor = ServiceLocator.Get<IResourceExtractor>();
                        _resourceExtractorPool.Add(resourceExtractor);
                        _statistics.CreatedResourceExtractorCount++;
                    }
                }
                void PreCreateResourceVerifiers()
                {
                    for (var resourceVerifierId = 0; resourceVerifierId < configurations.ResourceVerifierCount; resourceVerifierId++)
                    {
                        var resourceVerifier = ServiceLocator.Get<IResourceVerifier>();
                        _resourceVerifierPool.Add(resourceVerifier);
                        _statistics.CreatedResourceVerifierCount++;
                    }
                }
            }
            void CreateAndDestroyHtmlRenderersAdaptively()
            {
                hardwareMonitor.OnLowCpuAndMemoryUsage += (averageCpuUsage, memoryUsage) =>
                {
                    if (_htmlRendererPool.Count > 0 || _statistics.CreatedHtmlRendererCount == configurations.MaxHtmlRendererCount) return;
                    CreateHtmlRenderer();

                    var createdHtmlRendererCount = _statistics.CreatedHtmlRendererCount;
                    _logger.LogInfo(
                        $"Low CPU usage ({averageCpuUsage}%) and low memory usage ({memoryUsage}%) detected. " +
                        $"Browser count increased from {createdHtmlRendererCount - 1} to {createdHtmlRendererCount}."
                    );
                };
                hardwareMonitor.OnHighCpuOrMemoryUsage += (averageCpuUsage, memoryUsage) =>
                {
                    if (_statistics.CreatedHtmlRendererCount == 1) return;
                    _htmlRendererPool.Take().Dispose();
                    _statistics.CreatedHtmlRendererCount--;

                    if (averageCpuUsage == null && memoryUsage == null)
                        throw new ArgumentException(nameof(averageCpuUsage), nameof(memoryUsage));

                    var createdHtmlRendererCount = _statistics.CreatedHtmlRendererCount;
                    if (averageCpuUsage != null && memoryUsage != null)
                        _logger.LogInfo(
                            $"High CPU usage ({averageCpuUsage}%) and high memory usage ({memoryUsage}%) detected. " +
                            $"Browser count decreased from {createdHtmlRendererCount + 1} to {createdHtmlRendererCount}."
                        );
                    else if (averageCpuUsage != null)
                        _logger.LogInfo(
                            $"High CPU usage ({averageCpuUsage}%) detected. " +
                            $"Browser count decreased from {createdHtmlRendererCount + 1} to {createdHtmlRendererCount}."
                        );
                    else
                        _logger.LogInfo(
                            $"High memory usage ({memoryUsage}%) detected. " +
                            $"Browser count decreased from {createdHtmlRendererCount + 1} to {createdHtmlRendererCount}."
                        );
                };
            }
            void CreateHtmlRenderer()
            {
                var htmlRenderer = ServiceLocator.Get<IHtmlRenderer>();
                htmlRenderer.OnResourceCaptured += memory.MemorizeToBeVerifiedResource;
                _htmlRendererPool.Add(htmlRenderer);
                _statistics.CreatedHtmlRendererCount++;
            }
        }

        public void Dispose()
        {
            if (_objectDisposed) return;
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
            _objectDisposed = true;
        }

        public IHtmlRenderer GetHtmlRenderer(CancellationToken cancellationToken)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(NetworkServicePool));
            return _htmlRendererPool.Take(cancellationToken);
        }

        public IResourceExtractor GetResourceExtractor(CancellationToken cancellationToken)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(NetworkServicePool));
            return _resourceExtractorPool.Take(cancellationToken);
        }

        public IResourceVerifier GetResourceVerifier(CancellationToken cancellationToken)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(NetworkServicePool));
            return _resourceVerifierPool.Take(cancellationToken);
        }

        public void Return(IResourceExtractor resourceExtractor)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(NetworkServicePool));
            _resourceExtractorPool.Add(resourceExtractor);
        }

        public void Return(IResourceVerifier resourceVerifier)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(NetworkServicePool));
            _resourceVerifierPool.Add(resourceVerifier);
        }

        public void Return(IHtmlRenderer htmlRenderer)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(NetworkServicePool));
            _htmlRendererPool.Add(htmlRenderer);
        }

        void ReleaseUnmanagedResources()
        {
            DisposeResourceExtractorPool();
            DisposeResourceVerifierPool();
            DisposeHtmlRendererPool();
            CheckForOrphanedResources();

            void DisposeResourceExtractorPool()
            {
                while (_resourceExtractorPool?.Any() ?? false)
                {
                    _resourceExtractorPool.Take();
                    _statistics.DisposedResourceExtractorCount++;
                }
                _resourceExtractorPool?.Dispose();
                _resourceExtractorPool = null;
            }
            void DisposeResourceVerifierPool()
            {
                while (_resourceVerifierPool?.Any() ?? false)
                {
                    _resourceVerifierPool.Take().Dispose();
                    _statistics.DisposedResourceVerifierCount++;
                }
                _resourceVerifierPool?.Dispose();
                _resourceVerifierPool = null;
            }
            void DisposeHtmlRendererPool()
            {
                while (_htmlRendererPool?.Any() ?? false)
                {
                    _htmlRendererPool.Take().Dispose();
                    _statistics.DisposedHtmlRendererCount++;

                    var disposedHtmlRendererCount = _statistics.DisposedHtmlRendererCount;
                    var createdHtmlRendererCount = _statistics.CreatedHtmlRendererCount;
                    _eventBroadcaster.Broadcast(new Event
                    {
                        EventType = EventType.StopProgressUpdated,
                        Message = $"Closing web browsers ({disposedHtmlRendererCount}/{createdHtmlRendererCount}) ..."
                    });
                }
                _htmlRendererPool?.Dispose();
                _htmlRendererPool = null;
            }
            void CheckForOrphanedResources()
            {
                var orphanedResourceErrorMessage = string.Empty;
                if (_statistics.DisposedResourceExtractorCount != _statistics.CreatedResourceExtractorCount)
                    orphanedResourceErrorMessage += GetErrorMessage(
                        _statistics.CreatedResourceExtractorCount,
                        nameof(ResourceExtractor),
                        _statistics.DisposedResourceExtractorCount
                    );

                if (_statistics.DisposedResourceVerifierCount != _statistics.CreatedResourceVerifierCount)
                    orphanedResourceErrorMessage += GetErrorMessage(
                        _statistics.CreatedResourceVerifierCount,
                        nameof(ResourceVerifier),
                        _statistics.DisposedResourceVerifierCount
                    );

                if (_statistics.DisposedHtmlRendererCount != _statistics.CreatedHtmlRendererCount)
                    orphanedResourceErrorMessage += GetErrorMessage(
                        _statistics.CreatedHtmlRendererCount,
                        nameof(HtmlRenderer),
                        _statistics.DisposedHtmlRendererCount
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

        struct Statistics
        {
            public int CreatedHtmlRendererCount { get; set; }

            public int CreatedResourceExtractorCount { get; set; }

            public int CreatedResourceVerifierCount { get; set; }

            public int DisposedHtmlRendererCount { get; set; }

            public int DisposedResourceExtractorCount { get; set; }

            public int DisposedResourceVerifierCount { get; set; }
        }

        ~NetworkServicePool() { ReleaseUnmanagedResources(); }
    }
}