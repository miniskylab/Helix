using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Helix.Bot.Abstractions;
using Helix.Core;
using Helix.WebBrowser.Abstractions;
using log4net;
using Titanium.Web.Proxy.EventArguments;

namespace Helix.Bot
{
    public sealed class HtmlRenderer : IHtmlRenderer
    {
        int _activeHttpTrafficCount;
        BlockingCollection<Resource> _capturedResources;
        CancellationTokenSource _networkTrafficCts;
        bool _objectDisposed;
        Resource _resourceBeingRendered;
        bool _takeScreenshot;
        bool _uriBeingRenderedWasFoundInCapturedNetworkTraffic;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public HtmlRenderer(Configurations configurations, IResourceScope resourceScope, ILog log, IWebBrowser webBrowser,
            IHttpContentTypeToResourceTypeDictionary httpContentTypeToResourceTypeDictionary)
        {
            _log = log;
            _webBrowser = webBrowser;
            _configurations = configurations;
            _webBrowser.BeforeRequest += EnsureInternal;
            _webBrowser.BeforeResponse += CaptureNetworkTraffic;

            _objectDisposed = false;
            _takeScreenshot = false;

            #region Local Functions

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
                var request = networkTraffic.HttpClient.Request;
                var response = networkTraffic.HttpClient.Response;
                return Task.Run(() =>
                {
                    Interlocked.Increment(ref _activeHttpTrafficCount);
                    try
                    {
                        if (request.Method.ToUpperInvariant() != "GET") return;
                        if (!_uriBeingRenderedWasFoundInCapturedNetworkTraffic)
                        {
                            if (!TryFindUriBeingRendered()) return;
                            if (TryFollowRedirects()) return;

                            UpdateStatusCodeIfChanged();
                            TakeScreenshotIfConfigured();
                            _uriBeingRenderedWasFoundInCapturedNetworkTraffic = true;
                            return;
                        }

                        if (_resourceBeingRendered.StatusCode.IsWithinBrokenRange() || IsRedirectResponse()) return;
                        _capturedResources.Add(new Resource
                        {
                            ParentUri = _resourceBeingRendered.Uri,
                            Uri = request.RequestUri.StripFragment(),
                            OriginalUrl = request.Url,
                            OriginalUri = request.RequestUri,
                            Size = response.ContentLength,
                            StatusCode = (StatusCode) response.StatusCode,
                            ResourceType = httpContentTypeToResourceTypeDictionary[response.ContentType]
                        });
                    }
                    finally { Interlocked.Decrement(ref _activeHttpTrafficCount); }
                }, _networkTrafficCts.Token);

                #region Local Functions

                bool TryFollowRedirects()
                {
                    if (!IsRedirectResponse()) return false;
                    if (!response.Headers.Headers.TryGetValue("Location", out var locationHeader))
                    {
                        _log.Info(
                            "Http redirect response without \"Location\" header detected in captured resources while rendering: " +
                            $"{_resourceBeingRendered.ToJson()}"
                        );
                        return false;
                    }

                    if (!Uri.TryCreate(locationHeader.Value, UriKind.RelativeOrAbsolute, out var redirectUri)) return false;
                    _resourceBeingRendered.Uri = redirectUri.IsAbsoluteUri
                        ? redirectUri.StripFragment()
                        : new Uri(_resourceBeingRendered.Uri, redirectUri).StripFragment();

                    return true;
                }
                bool TryFindUriBeingRendered()
                {
                    var capturedUri = request.RequestUri.StripFragment();
                    var bothSchemesAreNotEqual = !capturedUri.Scheme.Equals(_resourceBeingRendered.Uri.Scheme);
                    var strictTransportSecurity = _resourceBeingRendered.Uri.Scheme == "http" && capturedUri.Scheme == "https";
                    if (bothSchemesAreNotEqual && !strictTransportSecurity) return false;
                    return RemoveScheme(capturedUri).Equals(RemoveScheme(_resourceBeingRendered.Uri));
                }
                void UpdateStatusCodeIfChanged()
                {
                    var newStatusCode = response.StatusCode;
                    var oldStatusCode = (int) _resourceBeingRendered.StatusCode;
                    if (newStatusCode == oldStatusCode) return;

                    _resourceBeingRendered.StatusCode = (StatusCode) newStatusCode;
                    log.Debug($"StatusCode changed from [{oldStatusCode}] to [{newStatusCode}] at [{_resourceBeingRendered.Uri}]");
                }
                void TakeScreenshotIfConfigured()
                {
                    if (_resourceBeingRendered.StatusCode.IsWithinBrokenRange() && configurations.TakeScreenshotEvidence)
                        _takeScreenshot = true;
                }
                bool IsRedirectResponse() { return 300 <= response.StatusCode && response.StatusCode < 400; }
                string RemoveScheme(Uri uri) { return WebUtility.UrlDecode($"{uri.Host}:{uri.Port}{uri.PathAndQuery}"); }

                #endregion
            }

            #endregion
        }

        public void Dispose()
        {
            if (_objectDisposed) return;
            _capturedResources?.Dispose();
            _networkTrafficCts?.Dispose();
            _webBrowser?.Dispose();
            _objectDisposed = true;
        }

        public bool TryRender(Resource resource, out string html, out long? millisecondsPageLoadTime, out List<Resource> capturedResources,
            CancellationToken cancellationToken)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(HtmlRenderer));
            EnsureNetworkTrafficIsHalted();

            try
            {
                _resourceBeingRendered = resource;
                _networkTrafficCts = new CancellationTokenSource();
                _capturedResources = new BlockingCollection<Resource>();
                _uriBeingRenderedWasFoundInCapturedNetworkTraffic = false;

                var renderingResult = _webBrowser.TryRender(resource.Uri, out html, out millisecondsPageLoadTime, cancellationToken);
                if (resource.StatusCode.IsWithinBrokenRange()) millisecondsPageLoadTime = null;
                if (!_takeScreenshot) return renderingResult;

                var pathToDirectoryContainsScreenshotFiles = _configurations.PathToDirectoryContainsScreenshotFiles;
                var pathToScreenshotFile = Path.Combine(pathToDirectoryContainsScreenshotFiles, $"{_resourceBeingRendered.Id}.png");
                if (!_webBrowser.TryTakeScreenshot(pathToScreenshotFile))
                    _log.Error($"Failed to take screenshot at URL: {_resourceBeingRendered.GetAbsoluteUrl()}");

                _takeScreenshot = false;
                return renderingResult;
            }
            finally
            {
                capturedResources = _capturedResources.ToList();
                _capturedResources.Dispose();
                _capturedResources = null;
            }

            #region Local Functions

            void EnsureNetworkTrafficIsHalted()
            {
                _networkTrafficCts?.Cancel();
                while (_activeHttpTrafficCount > 0) Thread.Sleep(100);
                _networkTrafficCts?.Dispose();
            }

            #endregion
        }

        #region Injected Services

        readonly ILog _log;
        readonly IWebBrowser _webBrowser;
        readonly Configurations _configurations;

        #endregion
    }
}