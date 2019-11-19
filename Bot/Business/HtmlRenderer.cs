using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public HtmlRenderer(Configurations configurations, IResourceScope resourceScope, ILog log, IWebBrowser webBrowser,
            IHttpContentTypeToResourceTypeDictionary httpContentTypeToResourceTypeDictionary)
        {
            _log = log;
            _webBrowser = webBrowser;
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
                            if (TryFollowRedirects()) return;
                            if (!TryFindUriBeingRendered()) return;

                            UpdateStatusCodeIfChanged();
                            TakeScreenshotIfConfigured();
                            _uriBeingRenderedWasFoundInCapturedNetworkTraffic = true;
                            return;
                        }

                        if (_resourceBeingRendered.StatusCode.IsWithinBrokenRange()) return;
                        _capturedResources.Add(new Resource
                        {
                            ParentUri = _resourceBeingRendered.Uri,
                            Uri = request.RequestUri,
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

                bool TryFollowRedirects() { return 300 <= response.StatusCode && response.StatusCode < 400; }
                bool TryFindUriBeingRendered()
                {
                    var capturedUri = request.RequestUri;
                    var bothSchemesAreNotEqual = !capturedUri.Scheme.Equals(_resourceBeingRendered.Uri.Scheme);
                    var strictTransportSecurity = _resourceBeingRendered.Uri.Scheme == "http" && capturedUri.Scheme == "https";
                    if (bothSchemesAreNotEqual && !strictTransportSecurity) return false;

                    var uriBeingRendered = _resourceBeingRendered.Uri;
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
                void TakeScreenshotIfConfigured()
                {
                    if (_resourceBeingRendered.StatusCode.IsWithinBrokenRange() && configurations.TakeScreenshotEvidence)
                        _takeScreenshot = true;
                }

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

                var pathToDirectoryContainsScreenshotFiles = Configurations.PathToDirectoryContainsScreenshotFiles;
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

        #endregion
    }
}