using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Helix.Bot.Abstractions;
using Helix.WebBrowser.Abstractions;
using log4net;
using Titanium.Web.Proxy.EventArguments;

namespace Helix.Bot
{
    public sealed class HtmlRenderer : IHtmlRenderer
    {
        int _activeHttpTrafficCount;
        readonly ILog _log;
        CancellationTokenSource _networkTrafficCts;
        bool _objectDisposed;
        Resource _resourceBeingRendered;
        bool _takeScreenshot;
        readonly IWebBrowser _webBrowser;

        public event Action<Resource> OnResourceCaptured;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public HtmlRenderer(Configurations configurations, IResourceScope resourceScope, ILog log, IWebBrowser webBrowser,
            IHttpContentTypeToResourceTypeDictionary httpContentTypeToResourceTypeDictionary)
        {
            _log = log;
            _webBrowser = webBrowser;
            _webBrowser.BeforeRequest += EnsureInternal;
            _webBrowser.BeforeResponse += CaptureNetworkTraffic;

            _objectDisposed = false;
            _takeScreenshot = false;

            Task EnsureInternal(object _, SessionEventArgs networkTraffic)
            {
                return Task.Run(() =>
                {
                    Interlocked.Increment(ref _activeHttpTrafficCount);
                    try
                    {
                        networkTraffic.HttpClient.Request.RequestUri = resourceScope.Localize(networkTraffic.HttpClient.Request.RequestUri);
                        networkTraffic.HttpClient.Request.Host = networkTraffic.HttpClient.Request.RequestUri.Host;
                    }
                    finally { Interlocked.Decrement(ref _activeHttpTrafficCount); }
                }, _networkTrafficCts.Token);
            }
            Task CaptureNetworkTraffic(object _, SessionEventArgs networkTraffic)
            {
                var parentUri = _webBrowser.CurrentUri;
                var request = networkTraffic.HttpClient.Request;
                var response = networkTraffic.HttpClient.Response;
                var uriBeingRendered = _resourceBeingRendered.Uri;
                return Task.Run(() =>
                {
                    Interlocked.Increment(ref _activeHttpTrafficCount);
                    try
                    {
                        if (request.Method.ToUpperInvariant() != "GET") return;
                        if (TryFollowRedirects()) return;
                        if (ParentUriWasFound())
                        {
                            UpdateStatusCodeIfChanged();
                            TakeScreenshotIfNecessary();
                            return;
                        }

                        if (_resourceBeingRendered.StatusCode.IsWithinBrokenRange()) return;
                        OnResourceCaptured?.Invoke(new Resource
                        {
                            ParentUri = parentUri,
                            OriginalUrl = request.Url,
                            Uri = request.RequestUri,
                            StatusCode = (StatusCode) response.StatusCode,
                            Size = response.ContentLength,
                            ResourceType = httpContentTypeToResourceTypeDictionary[response.ContentType]
                        });
                    }
                    finally { Interlocked.Decrement(ref _activeHttpTrafficCount); }
                }, _networkTrafficCts.Token);

                #region Local Functions

                bool TryFollowRedirects()
                {
                    if (response.StatusCode < 300 || 400 <= response.StatusCode) return false;
                    if (!response.Headers.Headers.TryGetValue("Location", out var locationHeader)) return false;
                    if (!Uri.TryCreate(locationHeader.Value, UriKind.RelativeOrAbsolute, out var redirectUri)) return false;
                    uriBeingRendered = redirectUri.IsAbsoluteUri ? redirectUri : new Uri(_resourceBeingRendered.Uri, redirectUri);
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
                void UpdateStatusCodeIfChanged()
                {
                    var newStatusCode = response.StatusCode;
                    var oldStatusCode = (int) _resourceBeingRendered.StatusCode;
                    if (newStatusCode == oldStatusCode) return;

                    _resourceBeingRendered.StatusCode = (StatusCode) newStatusCode;
                    log.Info($"StatusCode changed from [{oldStatusCode}] to [{newStatusCode}] at [{_resourceBeingRendered.Uri}]");
                }
                void TakeScreenshotIfNecessary()
                {
                    if (_resourceBeingRendered.StatusCode.IsWithinBrokenRange() && configurations.TakeScreenshotEvidence)
                        _takeScreenshot = true;
                }

                #endregion
            }
        }

        public void Dispose()
        {
            if (_objectDisposed) return;
            _networkTrafficCts?.Dispose();
            _webBrowser?.Dispose();
            _objectDisposed = true;
        }

        public bool TryRender(Resource resource, out string html, out long? millisecondsPageLoadTime, CancellationToken cancellationToken)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(HtmlRenderer));
            EnsureNetworkTrafficIsHalted();

            _networkTrafficCts = new CancellationTokenSource();
            _resourceBeingRendered = resource;

            var uri = resource.Uri;
            var renderingResult = _webBrowser.TryRender(uri, out html, out millisecondsPageLoadTime, cancellationToken);

            if (resource.StatusCode.IsWithinBrokenRange()) millisecondsPageLoadTime = null;
            if (!_takeScreenshot) return renderingResult;

            var pathToDirectoryContainsScreenshotFiles = Configurations.PathToDirectoryContainsScreenshotFiles;
            var pathToScreenshotFile = Path.Combine(pathToDirectoryContainsScreenshotFiles, $"{_resourceBeingRendered.Id}.png");
            if (!_webBrowser.TryTakeScreenshot(pathToScreenshotFile))
                _log.Error($"Failed to take screenshot at URL: {_resourceBeingRendered.GetAbsoluteUrl()}");

            _takeScreenshot = false;
            return renderingResult;

            void EnsureNetworkTrafficIsHalted()
            {
                _networkTrafficCts?.Cancel();
                while (_activeHttpTrafficCount > 0) Thread.Sleep(100);
                _networkTrafficCts?.Dispose();
            }
        }
    }
}