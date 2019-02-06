using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Crawler
{
    public class ServicePool : IServicePool
    {
        const int RawResourceExtractorCount = 300;
        const int RawResourceVerifierCount = 2500;
        readonly object _disposalSyncRoot;
        readonly ILogger _logger;
        readonly IMemory _memory;
        BlockingCollection<IRawResourceExtractor> _rawResourceExtractorPool;
        BlockingCollection<IRawResourceVerifier> _rawResourceVerifierPool;
        BlockingCollection<IWebBrowser> _webBrowserPool;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ServicePool(IMemory memory, ILogger logger)
        {
            _memory = memory;
            _logger = logger;
            _disposalSyncRoot = new object();
            _rawResourceExtractorPool = new BlockingCollection<IRawResourceExtractor>();
            _rawResourceVerifierPool = new BlockingCollection<IRawResourceVerifier>();
            _webBrowserPool = new BlockingCollection<IWebBrowser>();
        }

        public void Dispose()
        {
            lock (_disposalSyncRoot)
            {
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
            }
        }

        public void EnsureEnoughResources(CancellationToken cancellationToken)
        {
            InitializeRawResourceExtractorPool();
            InitializeRawResourceVerifierPool();
            InitializeWebBrowserPool();

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
            void InitializeWebBrowserPool()
            {
                Parallel.For(0, _memory.Configurations.WebBrowserCount, webBrowserId =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var webBrowser = ServiceLocator.Get<IWebBrowser>();
                    webBrowser.OnRawResourceCaptured += rawResource => _memory.Memorize(rawResource, CancellationToken.None);
                    _webBrowserPool.Add(webBrowser, cancellationToken);
                });
            }
        }

        public IRawResourceExtractor GetRawResourceExtractor(CancellationToken cancellationToken)
        {
            return _rawResourceExtractorPool.Take(cancellationToken);
        }

        public IRawResourceVerifier GetResourceVerifier(CancellationToken cancellationToken)
        {
            return _rawResourceVerifierPool.Take(cancellationToken);
        }

        public IWebBrowser GetWebBrowser(CancellationToken cancellationToken) { return _webBrowserPool.Take(cancellationToken); }

        public void Return(IRawResourceExtractor rawResourceExtractor) { _rawResourceExtractorPool.Add(rawResourceExtractor); }

        public void Return(IRawResourceVerifier rawResourceVerifier) { _rawResourceVerifierPool.Add(rawResourceVerifier); }

        public void Return(IWebBrowser webBrowser) { _webBrowserPool.Add(webBrowser); }

        void ReleaseUnmanagedResources()
        {
            var disposedRawResourceExtractorCount = 0;
            var disposedRawResourceVerifierCount = 0;
            var disposedWebBrowserCount = 0;
            DisposeRawResourceExtractorPool();
            DisposeRawResourceVerifierPool();
            DisposeWebBrowserPool();

            _rawResourceExtractorPool?.Dispose();
            _rawResourceVerifierPool?.Dispose();
            _webBrowserPool?.Dispose();

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