using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;
using Helix.WebBrowser.Abstractions;
using Titanium.Web.Proxy.EventArguments;

namespace Helix.Crawler
{
    public class HtmlRenderer : IHtmlRenderer
    {
        readonly ILogger _logger;
        bool _objectDisposed;
        readonly Dictionary<string, object> _publicApiLockMap;
        Resource _resourceBeingRendered;
        bool _takeScreenshot;
        bool _theFirstNoneRedirectResponseWasConsumed;
        IWebBrowser _webBrowser;

        public event Action<RawResource> OnRawResourceCaptured;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public HtmlRenderer(Configurations configurations, IWebBrowserProvider webBrowserProvider, IResourceScope resourceScope,
            IReportWriter reportWriter, ILogger logger)
        {
            var httpResponseConsumptionLock = new object();
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
            _theFirstNoneRedirectResponseWasConsumed = false;
            _publicApiLockMap = new Dictionary<string, object> { { $"{nameof(TryRender)}", new object() } };

            Task EnsureInternal(object _, SessionEventArgs networkTraffic)
            {
                return Task.Run(() =>
                {
                    networkTraffic.WebSession.Request.RequestUri = resourceScope.Localize(networkTraffic.WebSession.Request.RequestUri);
                    networkTraffic.WebSession.Request.Host = networkTraffic.WebSession.Request.RequestUri.Host;
                });
            }
            Task CaptureNetworkTraffic(object _, SessionEventArgs networkTraffic)
            {
                var parentUri = _webBrowser.CurrentUri;
                return Task.Run(() =>
                {
                    var request = networkTraffic.WebSession.Request;
                    var response = networkTraffic.WebSession.Response;
                    lock (httpResponseConsumptionLock)
                    {
                        if (!_theFirstNoneRedirectResponseWasConsumed)
                        {
                            if (ThisIsAValidRedirectResponse(out var destinationUri))
                            {
                                _resourceBeingRendered.Uri = destinationUri;
                                return;
                            }
                            UpdateStatusCodeIfNotMatch();

                            var resourceIsBroken = (int) _resourceBeingRendered.HttpStatusCode >= 400;
                            if (resourceIsBroken && configurations.TakeScreenshotEvidence) _takeScreenshot = true;
                            _theFirstNoneRedirectResponseWasConsumed = true;

                            bool ThisIsAValidRedirectResponse(out Uri redirectUri)
                            {
                                redirectUri = null;
                                var isNotRedirectResponse = response.StatusCode < 300 || 400 <= response.StatusCode;
                                if (isNotRedirectResponse || !response.Headers.Headers.TryGetValue("Location", out var locationHeader))
                                    return false;

                                if (Uri.TryCreate(locationHeader.Value, UriKind.RelativeOrAbsolute, out redirectUri))
                                {
                                    if (redirectUri.IsAbsoluteUri) return true;
                                    var baseUri = new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority));
                                    redirectUri = new Uri(baseUri, redirectUri);
                                    return true;
                                }

                                logger.LogInfo($"Invalid redirect at: [{_resourceBeingRendered.Uri}]");
                                return false;
                            }
                            void UpdateStatusCodeIfNotMatch()
                            {
                                if (response.StatusCode == (int) _resourceBeingRendered.HttpStatusCode) return;

                                var uri = _resourceBeingRendered.Uri;
                                var oldStatusCode = (int) _resourceBeingRendered.HttpStatusCode;
                                var newStatusCode = response.StatusCode;
                                logger.LogInfo($"StatusCode changed from [{oldStatusCode}] to [{newStatusCode}] at [{uri}]");

                                _resourceBeingRendered.HttpStatusCode = (HttpStatusCode) response.StatusCode;
                                reportWriter.UpdateStatusCode(_resourceBeingRendered.Id, (HttpStatusCode) response.StatusCode);
                            }
                        }
                    }

                    if (response.ContentType == null) return;
                    var isNotGETRequest = request.Method.ToUpperInvariant() != "GET";
                    var isNotCss = !response.ContentType.StartsWith("text/css", StringComparison.OrdinalIgnoreCase);
                    var isNotImage = !response.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
                    var isNotAudio = !response.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
                    var isNotVideo = !response.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
                    var isNotFont = !response.ContentType.StartsWith("font/", StringComparison.OrdinalIgnoreCase);
                    var isNotJavaScript = !response.ContentType.StartsWith("application/javascript", StringComparison.OrdinalIgnoreCase) &&
                                          !response.ContentType.StartsWith("application/ecmascript", StringComparison.OrdinalIgnoreCase);
                    if (!isNotGETRequest || isNotCss && isNotFont && isNotJavaScript && isNotImage && isNotAudio && isNotVideo) return;
                    OnRawResourceCaptured?.Invoke(new RawResource
                    {
                        ParentUri = parentUri,
                        Url = request.Url,
                        HttpStatusCode = (HttpStatusCode) response.StatusCode
                    });
                });
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
                _theFirstNoneRedirectResponseWasConsumed = false;
                _resourceBeingRendered = resource;

                var uri = resource.Uri;
                var renderingResult = _webBrowser.TryRender(uri, out html, out pageLoadTime, cancellationToken, attemptCount, onFailed);
                if (!_takeScreenshot) return renderingResult;

                _takeScreenshot = false;
                _webBrowser.TryTakeScreenshot($"screenshots\\{_resourceBeingRendered.Id}.png", OnScreenshotTakingFailed);
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