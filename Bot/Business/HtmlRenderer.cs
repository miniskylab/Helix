using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
        BlockingCollection<Resource> _capturedResources;
        CancellationTokenSource _networkTrafficCts;
        bool _objectDisposed;
        Resource _resourceBeingRendered;
        bool _takeScreenshot;
        bool _uriBeingRenderedWasFoundInCapturedNetworkTraffic;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public HtmlRenderer(Configurations configurations, IResourceScope resourceScope, IIncrementalIdGenerator incrementalIdGenerator,
            IHttpContentTypeToResourceTypeDictionary httpContentTypeToResourceTypeDictionary, ILog log, IWebBrowser webBrowser)
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
                var originalResponseStatusCode = response.StatusCode;
                return Task.Run(() =>
                {
                    Interlocked.Increment(ref _activeHttpTrafficCount);
                    try
                    {
                        if (request.Method.ToUpperInvariant() != "GET") return;

                        var resourceSize = response.ContentLength;
                        var resourceType = httpContentTypeToResourceTypeDictionary[response.ContentType];
                        if (!_uriBeingRenderedWasFoundInCapturedNetworkTraffic)
                        {
                            if (!TryFindUriBeingRendered()) return;
                            if (TryFollowRedirects()) return;

                            UpdateStatusCodeIfChanged();
                            TakeScreenshotIfConfigured();
                            _uriBeingRenderedWasFoundInCapturedNetworkTraffic = true;

                            var resourceSizeIsTooBig = resourceSize / 1024f / 1024f > Configurations.RenderableResourceSizeInMb;
                            var resourceTypeIsNotRenderable = !(ResourceType.Html | ResourceType.Unknown).HasFlag(resourceType);
                            var responseStatusCodeIsOk = originalResponseStatusCode == (int) StatusCode.Ok;
                            if (responseStatusCodeIsOk && (resourceSizeIsTooBig || resourceTypeIsNotRenderable))
                                response.StatusCode = (int) StatusCode.NoContent;

                            return;
                        }

                        if (_resourceBeingRendered.StatusCode.IsWithinBrokenRange()) return;
                        if (!configurations.IncludeRedirectUrlsInReport && IsRedirectResponse()) return;
                        _capturedResources.Add(
                            new Resource
                            (
                                incrementalIdGenerator.GetNext(),
                                request.Url,
                                _resourceBeingRendered.Uri,
                                false
                            )
                            {
                                Size = resourceSize,
                                Uri = request.RequestUri,
                                ResourceType = resourceType,
                                StatusCode = (StatusCode) originalResponseStatusCode
                            }
                        );
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
                    _resourceBeingRendered.Uri = redirectUri.IsAbsoluteUri ? redirectUri : new Uri(_resourceBeingRendered.Uri, redirectUri);

                    return true;
                }
                bool TryFindUriBeingRendered()
                {
                    var capturedUri = request.RequestUri;
                    var bothSchemesAreNotEqual = !capturedUri.Scheme.Equals(_resourceBeingRendered.Uri.Scheme);
                    var strictTransportSecurity = _resourceBeingRendered.Uri.Scheme == "http" && capturedUri.Scheme == "https";
                    if (bothSchemesAreNotEqual && !strictTransportSecurity) return false;
                    return RemoveScheme(capturedUri).Equals(RemoveScheme(_resourceBeingRendered.Uri));
                }
                void UpdateStatusCodeIfChanged()
                {
                    var newStatusCode = originalResponseStatusCode;
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
                bool IsRedirectResponse() { return 300 <= originalResponseStatusCode && originalResponseStatusCode < 400; }
                static string RemoveScheme(Uri uri) { return WebUtility.UrlDecode($"{uri.Host}:{uri.Port}{uri.PathAndQuery}"); }

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
                if (!_uriBeingRenderedWasFoundInCapturedNetworkTraffic)
                    _log.Error(
                        $"Uri being rendered was not found in captured network traffic while rendering: {resource.ToJson()}\n" +
                        "-----> The url being rendered may contains unicode characters which causes this issue."
                    );

                if (resource.StatusCode.IsWithinBrokenRange()) millisecondsPageLoadTime = null;
                if (!_takeScreenshot) return renderingResult;

                var pathToDirectoryContainsScreenshotFiles = _configurations.PathToDirectoryContainsScreenshotFiles;
                var pathToScreenshotFile = Path.Combine(pathToDirectoryContainsScreenshotFiles, $"{resource.Id}.png");
                if (!_webBrowser.TryTakeScreenshot(pathToScreenshotFile))
                    _log.Error($"Failed to take screenshot at URL: {resource.Uri.AbsoluteUri}");

                _takeScreenshot = false;
                return renderingResult;
            }
            finally
            {
                capturedResources = _capturedResources.ToList();
                _capturedResources.Dispose();
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