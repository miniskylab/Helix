using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;
using Helix.WebBrowser.Abstractions;
using Titanium.Web.Proxy.EventArguments;

namespace Helix.Crawler
{
    public class HtmlRenderer : IHtmlRenderer
    {
        int _activeHttpTrafficCount;
        CancellationTokenSource _cancellationTokenSource;
        readonly ILogger _logger;
        bool _objectDisposed;
        readonly string _pathToDirectoryContainsScreenshotFiles;
        readonly Dictionary<string, object> _publicApiLockMap;
        Resource _resourceBeingRendered;
        bool _takeScreenshot;
        IWebBrowser _webBrowser;

        public event Action<Resource> OnResourceCaptured;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public HtmlRenderer(Configurations configurations, IWebBrowserProvider webBrowserProvider, IResourceScope resourceScope,
            IReportWriter reportWriter, IResourceProcessor resourceProcessor, ILogger logger)
        {
            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var pathToChromiumExecutable = Path.Combine(workingDirectory, "chromium/chrome.exe");
            _webBrowser = webBrowserProvider.GetWebBrowser(
                pathToChromiumExecutable,
                workingDirectory,
                configurations.UseIncognitoWebBrowser,
                configurations.UseHeadlessWebBrowsers,
                (1920, 1080)
            );
            _webBrowser.BeforeRequest += EnsureInternal;
            _webBrowser.BeforeResponse += CaptureNetworkTraffic;

            _logger = logger;
            _objectDisposed = false;
            _takeScreenshot = false;
            _pathToDirectoryContainsScreenshotFiles = Path.Combine(workingDirectory, "screenshots");
            _publicApiLockMap = new Dictionary<string, object> { { $"{nameof(TryRender)}", new object() } };

            EnsureDirectoryContainsScreenshotFilesIsRecreated();

            Task EnsureInternal(object _, SessionEventArgs networkTraffic)
            {
                return Task.Factory.StartNew(() =>
                {
                    Interlocked.Increment(ref _activeHttpTrafficCount);
                    try
                    {
                        networkTraffic.WebSession.Request.RequestUri = resourceScope.Localize(networkTraffic.WebSession.Request.RequestUri);
                        networkTraffic.WebSession.Request.Host = networkTraffic.WebSession.Request.RequestUri.Host;
                    }
                    finally { Interlocked.Decrement(ref _activeHttpTrafficCount); }
                }, _cancellationTokenSource.Token, TaskCreationOptions.None, PriorityTaskScheduler.Highest);
            }
            Task CaptureNetworkTraffic(object _, SessionEventArgs networkTraffic)
            {
                var parentUri = _webBrowser.CurrentUri;
                var request = networkTraffic.WebSession.Request;
                var response = networkTraffic.WebSession.Response;
                return Task.Factory.StartNew(() =>
                {
                    Interlocked.Increment(ref _activeHttpTrafficCount);
                    try
                    {
                        if (ParentUriWasFound())
                        {
                            UpdateStatusCodeIfNotMatch();
                            TakeScreenshotIfNecessary();
                        }

                        if (response.ContentType == null) return;
                        var isNotGETRequest = request.Method.ToUpperInvariant() != "GET";
                        var isNotCss = !response.ContentType.StartsWith("text/css", StringComparison.OrdinalIgnoreCase);
                        var isNotImage = !response.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
                        var isNotAudio = !response.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
                        var isNotVideo = !response.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
                        var isNotFont = !response.ContentType.StartsWith("font/", StringComparison.OrdinalIgnoreCase);
                        var isNotScript = !response.ContentType.StartsWith("application/javascript", StringComparison.OrdinalIgnoreCase) &&
                                          !response.ContentType.StartsWith("application/ecmascript", StringComparison.OrdinalIgnoreCase);
                        if (isNotGETRequest || isNotCss && isNotFont && isNotScript && isNotImage && isNotAudio && isNotVideo) return;
                        OnResourceCaptured?.Invoke(
                            resourceProcessor.Enrich(new Resource
                            {
                                ParentUri = parentUri,
                                OriginalUrl = request.Url,
                                Uri = request.RequestUri,
                                StatusCode = (StatusCode) response.StatusCode
                            })
                        );
                    }
                    finally { Interlocked.Decrement(ref _activeHttpTrafficCount); }
                }, _cancellationTokenSource.Token, TaskCreationOptions.None, PriorityTaskScheduler.Highest);

                bool ParentUriWasFound()
                {
                    if (request.Method.ToUpperInvariant() != "GET") return false;
                    var capturedUri = request.RequestUri;
                    var uriBeingRendered = _resourceBeingRendered.Uri;
                    var bothSchemesAreNotEqual = !capturedUri.Scheme.Equals(uriBeingRendered.Scheme);
                    var strictTransportSecurity = uriBeingRendered.Scheme == "http" && capturedUri.Scheme == "https";
                    if (bothSchemesAreNotEqual && !strictTransportSecurity) return false;

                    var capturedUriWithoutScheme = capturedUri.Host + capturedUri.PathAndQuery + capturedUri.Fragment;
                    var uriBeingRenderedWithoutScheme = uriBeingRendered.Host + uriBeingRendered.PathAndQuery + uriBeingRendered.Fragment;
                    return capturedUriWithoutScheme.Equals(uriBeingRenderedWithoutScheme);
                }
                void UpdateStatusCodeIfNotMatch()
                {
                    if (response.StatusCode == (int) _resourceBeingRendered.StatusCode) return;
                    if (response.StatusCode < 300 && 400 <= response.StatusCode)
                    {
                        var newStatusCode = response.StatusCode;
                        var oldStatusCode = (int) _resourceBeingRendered.StatusCode;
                        logger.LogInfo($"StatusCode changed from [{oldStatusCode}] to [{newStatusCode}] at [{_resourceBeingRendered.Uri}]");
                    }

                    _resourceBeingRendered.StatusCode = (StatusCode) response.StatusCode;
                    reportWriter.UpdateStatusCode(_resourceBeingRendered.Id, (StatusCode) response.StatusCode);
                }
                void TakeScreenshotIfNecessary()
                {
                    var resourceIsBroken = (int) _resourceBeingRendered.StatusCode >= 400;
                    if (resourceIsBroken && configurations.TakeScreenshotEvidence) _takeScreenshot = true;
                }
            }
            void EnsureDirectoryContainsScreenshotFilesIsRecreated()
            {
                if (Directory.Exists(_pathToDirectoryContainsScreenshotFiles))
                    Directory.Delete(_pathToDirectoryContainsScreenshotFiles, true);
                Directory.CreateDirectory(_pathToDirectoryContainsScreenshotFiles);
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

        public bool TryRender(Resource resource, out string html, out long? pageLoadTime, CancellationToken cancellationToken,
            int attemptCount = 3, Action<Exception> onFailed = null)
        {
            lock (_publicApiLockMap[nameof(TryRender)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(HtmlRenderer));
                _cancellationTokenSource?.Cancel();
                while (_activeHttpTrafficCount > 0) Thread.Sleep(100);
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
                _resourceBeingRendered = resource;

                var uri = resource.Uri;
                var renderingResult = _webBrowser.TryRender(uri, out html, out pageLoadTime, cancellationToken, attemptCount, onFailed);
                if (!_takeScreenshot) return renderingResult;

                var pathToScreenshotFile = Path.Combine(_pathToDirectoryContainsScreenshotFiles, $"{_resourceBeingRendered.Id}.png");
                _webBrowser.TryTakeScreenshot(pathToScreenshotFile, OnScreenshotTakingFailed);
                _takeScreenshot = false;

                return renderingResult;

                void OnScreenshotTakingFailed(Exception exception)
                {
                    _logger.LogInfo($"Failed to take screenshot of [{uri}].\r\n{exception}");
                }
            }
        }

        void ReleaseUnmanagedResources()
        {
            _webBrowser?.Dispose();
            _webBrowser = null;
        }

        ~HtmlRenderer() { ReleaseUnmanagedResources(); }
    }
}