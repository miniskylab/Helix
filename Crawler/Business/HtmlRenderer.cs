using System;
using System.IO;
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
        readonly ILogger _logger;
        CancellationTokenSource _networkTrafficCts;
        bool _objectDisposed;
        Resource _resourceBeingRendered;
        bool _takeScreenshot;
        IWebBrowser _webBrowser;

        public event Action<Resource> OnResourceCaptured;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public HtmlRenderer(Configurations configurations, IWebBrowser webBrowser, IResourceScope resourceScope, IReportWriter reportWriter,
            IResourceEnricher resourceEnricher, IHttpContentTypeToResourceTypeDictionary httpContentTypeToResourceTypeDictionary,
            ILogger logger)
        {
            _webBrowser = webBrowser;
            _webBrowser.BeforeRequest += EnsureInternal;
            _webBrowser.BeforeResponse += CaptureNetworkTraffic;

            _logger = logger;
            _objectDisposed = false;
            _takeScreenshot = false;

            Task EnsureInternal(object _, SessionEventArgs networkTraffic)
            {
                return Task.Factory.StartNew(() =>
                {
                    Interlocked.Increment(ref _activeHttpTrafficCount);
                    try
                    {
                        networkTraffic.HttpClient.Request.RequestUri = resourceScope.Localize(networkTraffic.HttpClient.Request.RequestUri);
                        networkTraffic.HttpClient.Request.Host = networkTraffic.HttpClient.Request.RequestUri.Host;
                    }
                    finally { Interlocked.Decrement(ref _activeHttpTrafficCount); }
                }, _networkTrafficCts.Token, TaskCreationOptions.None, PriorityTaskScheduler.Highest);
            }
            Task CaptureNetworkTraffic(object _, SessionEventArgs networkTraffic)
            {
                var parentUri = _webBrowser.CurrentUri;
                var request = networkTraffic.HttpClient.Request;
                var response = networkTraffic.HttpClient.Response;
                var uriBeingRendered = _resourceBeingRendered.Uri;
                return Task.Factory.StartNew(() =>
                {
                    Interlocked.Increment(ref _activeHttpTrafficCount);
                    try
                    {
                        if (request.Method.ToUpperInvariant() != "GET") return;
                        if (TryFollowRedirects()) return;
                        if (ParentUriWasFound())
                        {
                            UpdateStatusCodeIfNotMatch();
                            TakeScreenshotIfNecessary();
                            return;
                        }

                        if (_resourceBeingRendered.IsBroken) return;
                        var resource = new Resource
                        {
                            ParentUri = parentUri,
                            OriginalUrl = request.Url,
                            Uri = request.RequestUri,
                            StatusCode = (StatusCode) response.StatusCode,
                            Size = response.ContentLength,
                            ResourceType = httpContentTypeToResourceTypeDictionary[response.ContentType]
                        };
                        resourceEnricher.Enrich(resource);
                        OnResourceCaptured?.Invoke(resource);
                    }
                    finally { Interlocked.Decrement(ref _activeHttpTrafficCount); }
                }, _networkTrafficCts.Token, TaskCreationOptions.None, PriorityTaskScheduler.Highest);

                bool TryFollowRedirects()
                {
                    if (response.StatusCode < 300 || 400 <= response.StatusCode) return false;
                    if (!response.Headers.Headers.TryGetValue("Location", out var locationHeader)) return false;
                    if (!Uri.TryCreate(locationHeader.Value, UriKind.RelativeOrAbsolute, out var redirectUri)) return false;
                    uriBeingRendered = redirectUri.IsAbsoluteUri ? redirectUri : new Uri(_resourceBeingRendered.ParentUri, redirectUri);
                    return true;
                }
                bool ParentUriWasFound()
                {
                    var capturedUri = request.RequestUri;
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
                    var newStatusCode = response.StatusCode;
                    var oldStatusCode = (int) _resourceBeingRendered.StatusCode;
                    logger.LogInfo($"StatusCode changed from [{oldStatusCode}] to [{newStatusCode}] at [{_resourceBeingRendered.Uri}]");

                    _resourceBeingRendered.StatusCode = (StatusCode) response.StatusCode;
                    reportWriter.UpdateStatusCode(_resourceBeingRendered.Id, (StatusCode) response.StatusCode);
                }
                void TakeScreenshotIfNecessary()
                {
                    if (_resourceBeingRendered.IsBroken && configurations.TakeScreenshotEvidence)
                        _takeScreenshot = true;
                }
            }
        }

        public void Dispose()
        {
            if (_objectDisposed) return;
            _objectDisposed = true;

            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public bool TryRender(Resource resource, out string html, out long? millisecondsPageLoadTime, CancellationToken cancellationToken,
            Action<Exception> onFailed)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(HtmlRenderer));
            EnsureNetworkTrafficIsHalted();

            _networkTrafficCts = new CancellationTokenSource();
            _resourceBeingRendered = resource;

            var uri = resource.Uri;
            var renderingResult = _webBrowser.TryRender(uri, out html, out millisecondsPageLoadTime, cancellationToken, onFailed);

            if (resource.IsBroken) millisecondsPageLoadTime = null;
            if (!_takeScreenshot) return renderingResult;

            var pathToDirectoryContainsScreenshotFiles = Configurations.PathToDirectoryContainsScreenshotFiles;
            var pathToScreenshotFile = Path.Combine(pathToDirectoryContainsScreenshotFiles, $"{_resourceBeingRendered.Id}.png");
            _webBrowser.TryTakeScreenshot(pathToScreenshotFile, OnScreenshotTakingFailed);
            _takeScreenshot = false;

            return renderingResult;

            void EnsureNetworkTrafficIsHalted()
            {
                _networkTrafficCts?.Cancel();
                while (_activeHttpTrafficCount > 0) Thread.Sleep(100);
                _networkTrafficCts?.Dispose();
            }
            void OnScreenshotTakingFailed(Exception exception) { _logger.LogInfo($"Failed to take screenshot of [{uri}].\r\n-----> {exception}"); }
        }

        void ReleaseUnmanagedResources()
        {
            _webBrowser?.Dispose();
            _webBrowser = null;
        }

        ~HtmlRenderer() { ReleaseUnmanagedResources(); }
    }
}