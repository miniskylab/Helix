using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace Helix.Crawler
{
    public class ChromiumWebBrowser : IWebBrowser
    {
        ChromeDriver _chromeDriver;
        readonly Configurations _configurations;
        Uri _currentUri;
        ProxyServer _httpProxyServer;
        readonly List<int> _processIds;
        readonly IResourceScope _resourceScope;
        readonly object _syncRoot;

        public event Action OnIdle;
        public event Action<RawResource> OnRawResourceCaptured;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public ChromiumWebBrowser(Configurations configurations, IResourceScope resourceScope)
        {
            _configurations = configurations;
            _resourceScope = resourceScope;
            _processIds = new List<int>();
            _syncRoot = new object();
            SetupHttpProxyServer();
            Restart();
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
            }
        }

        public bool TryRender(Uri uri, Action<Exception> onFailed, CancellationToken cancellationToken, out string html)
        {
            _currentUri = uri ?? throw new ArgumentNullException(nameof(uri));
            if (onFailed == null) throw new ArgumentNullException(nameof(onFailed));

            html = null;
            var renderingFailedErrorMessage = $"Chromium web browser failed to render the URI: {uri}";
            lock (_syncRoot)
            {
                try
                {
                    if (!TryGoToUri(uri, 3, cancellationToken))
                    {
                        onFailed.Invoke(new TimeoutException(renderingFailedErrorMessage));
                        return false;
                    }

                    if (TryGetPageSource(out html)) return true;
                    onFailed.Invoke(new MemberAccessException($"Chromium web browser failed to obtain page source of the URI: {uri}"));
                    return false;
                }
                catch (OperationCanceledException operationCanceledException)
                {
                    if (operationCanceledException.CancellationToken != cancellationToken) throw;
                    onFailed.Invoke(new OperationCanceledException(renderingFailedErrorMessage, cancellationToken));
                    return false;
                }
                finally { OnIdle?.Invoke(); }
            }
        }

        void DisableNetwork()
        {
            _chromeDriver.NetworkConditions = new ChromeNetworkConditions
            {
                IsOffline = true,
                Latency = TimeSpan.FromTicks(1),
                UploadThroughput = long.MaxValue,
                DownloadThroughput = long.MaxValue
            };
        }

        void EnableNetwork()
        {
            _chromeDriver.NetworkConditions = new ChromeNetworkConditions
            {
                IsOffline = false,
                Latency = TimeSpan.FromTicks(1),
                UploadThroughput = long.MaxValue,
                DownloadThroughput = long.MaxValue
            };
        }

        void ForceQuit()
        {
            _processIds.ForEach(processId => Process.GetProcessById(processId).Kill());
            _processIds.Clear();
        }

        void ReleaseUnmanagedResources()
        {
            _httpProxyServer?.Stop();
            _httpProxyServer?.Dispose();
            _httpProxyServer = null;

            try { _chromeDriver?.Quit(); }
            catch (WebDriverException webDriverException)
            {
                if (webDriverException.InnerException.GetType() != typeof(WebException)) throw;
                ForceQuit();
            }
            _chromeDriver = null;
        }

        void Restart()
        {
            if (_chromeDriver != null)
            {
                ForceQuit();
                _chromeDriver = null;
            }

            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var chromeDriverService = ChromeDriverService.CreateDefaultService(workingDirectory);
            chromeDriverService.HideCommandPromptWindow = true;

            var chromeOptions = new ChromeOptions
            {
                BinaryLocation = Path.Combine(workingDirectory, "chromium/chrome.exe"),
                Proxy = new Proxy
                {
                    HttpProxy = $"http://{IPAddress.Loopback}:{_httpProxyServer.ProxyEndPoints[0].Port}",
                    FtpProxy = $"http://{IPAddress.Loopback}:{_httpProxyServer.ProxyEndPoints[0].Port}",
                    SslProxy = $"http://{IPAddress.Loopback}:{_httpProxyServer.ProxyEndPoints[0].Port}"
                }
            };
            if (_configurations.UseIncognitoWebBrowser) chromeOptions.AddArguments("--incognito");
            if (!_configurations.ShowWebBrowsers) chromeOptions.AddArguments("--headless");
            chromeOptions.AddArguments("--window-size=1920,1080");

            _chromeDriver = new ChromeDriver(chromeDriverService, chromeOptions)
            {
                NetworkConditions = new ChromeNetworkConditions
                {
                    IsOffline = true,
                    Latency = TimeSpan.MinValue,
                    UploadThroughput = long.MaxValue,
                    DownloadThroughput = long.MaxValue
                }
            };
            _processIds.Add(chromeDriverService.ProcessId);

            var childProcessQueryString = $"Select * From Win32_Process Where ParentProcessID={chromeDriverService.ProcessId}";
            var managementObjectSearcher = new ManagementObjectSearcher(childProcessQueryString);
            foreach (var managementObject in managementObjectSearcher.Get())
            {
                var processId = Convert.ToInt32(managementObject["ProcessID"]);
                _processIds.Add(processId);
            }
        }

        void SetupHttpProxyServer()
        {
            if (_httpProxyServer != null) return;
            _httpProxyServer = new ProxyServer();
            _httpProxyServer.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Loopback, 0));
            _httpProxyServer.Start();
            _httpProxyServer.BeforeRequest += EnsureInternal;
            _httpProxyServer.BeforeResponse += CaptureNetworkTraffic;

            Task CaptureNetworkTraffic(object _, SessionEventArgs networkTraffic)
            {
                var parentUri = _currentUri;
                return Task.Run(() =>
                {
                    var response = networkTraffic.WebSession.Response;
                    if (response.ContentType == null) return;

                    var request = networkTraffic.WebSession.Request;
                    var isNotGETRequest = request.Method.ToUpperInvariant() != "GET";
                    var isNotCss = !response.ContentType.StartsWith("text/css", StringComparison.OrdinalIgnoreCase);
                    var isNotImage = !response.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
                    var isNotAudio = !response.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
                    var isNotVideo = !response.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
                    var isNotFont = !response.ContentType.StartsWith("font/", StringComparison.OrdinalIgnoreCase);
                    var isNotJavaScript = !response.ContentType.StartsWith("application/javascript", StringComparison.OrdinalIgnoreCase) &&
                                          !response.ContentType.StartsWith("application/ecmascript", StringComparison.OrdinalIgnoreCase);
                    if (isNotGETRequest || isNotCss && isNotFont && isNotJavaScript && isNotImage && isNotAudio && isNotVideo) return;
                    OnRawResourceCaptured?.Invoke(new RawResource
                    {
                        ParentUri = parentUri,
                        Url = request.Url,
                        HttpStatusCode = response.StatusCode
                    });
                });
            }
            Task EnsureInternal(object _, SessionEventArgs networkTraffic)
            {
                return Task.Run(() =>
                {
                    networkTraffic.WebSession.Request.RequestUri = _resourceScope.Localize(networkTraffic.WebSession.Request.RequestUri);
                    networkTraffic.WebSession.Request.Host = networkTraffic.WebSession.Request.RequestUri.Host;
                });
            }
        }

        static bool TimeoutExceptionOccurred(Exception exception)
        {
            return exception is WebDriverTimeoutException ||
                   exception.InnerException is WebException webException && webException.Status == WebExceptionStatus.Timeout;
        }

        bool TryGetPageSource(out string html)
        {
            try { html = _chromeDriver.PageSource; }
            catch (WebDriverException webDriverException) when (TimeoutExceptionOccurred(webDriverException))
            {
                html = null;
                return false;
            }
            return true;
        }

        bool TryGoToUri(Uri uri, int attemptCount, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnableNetwork();
            for (var attemptNo = 0; attemptNo < attemptCount; attemptNo++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _chromeDriver.Navigate().GoToUrl(uri);
                    break;
                }
                catch (WebDriverException webDriverException) when (TimeoutExceptionOccurred(webDriverException))
                {
                    Restart();
                    if (attemptNo < attemptCount) continue;
                    return false;
                }
            }
            DisableNetwork();
            return true;
        }

        ~ChromiumWebBrowser() { ReleaseUnmanagedResources(); }
    }
}