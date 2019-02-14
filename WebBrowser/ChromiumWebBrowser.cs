using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Reflection;
using System.Threading;
using Helix.WebBrowser.Abstractions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace Helix.WebBrowser
{
    class ChromiumWebBrowser : IWebBrowser
    {
        ChromeDriver _chromeDriver;
        ProxyServer _httpProxyServer;
        readonly List<int> _processIds;
        readonly Stopwatch _stopwatch;
        readonly object _syncRoot;
        readonly bool _useHeadlessWebBrowser;
        readonly bool _useIncognitoWebBrowser;

        public event AsyncEventHandler<SessionEventArgs> BeforeRequest;
        public event AsyncEventHandler<SessionEventArgs> BeforeResponse;

        public ChromiumWebBrowser(bool useIncognitoWebBrowser, bool useHeadlessWebBrowser)
        {
            _processIds = new List<int>();
            _syncRoot = new object();
            _stopwatch = new Stopwatch();
            _useIncognitoWebBrowser = useIncognitoWebBrowser;
            _useHeadlessWebBrowser = useHeadlessWebBrowser;
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

        public bool TryRender(Uri uri, Action<Exception> onFailed, CancellationToken cancellationToken, out string html,
            out long? pageLoadTime)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (onFailed == null) throw new ArgumentNullException(nameof(onFailed));

            html = null;
            pageLoadTime = null;
            var renderingFailedErrorMessage = $"Chromium web browser failed to render the URI: {uri}";
            lock (_syncRoot)
            {
                try
                {
                    if (!TryGoToUri(3))
                    {
                        onFailed.Invoke(new TimeoutException(renderingFailedErrorMessage));
                        return false;
                    }

                    if (TryGetPageSource(out html))
                    {
                        pageLoadTime = _stopwatch.ElapsedMilliseconds;
                        return true;
                    }
                    onFailed.Invoke(new MemberAccessException($"Chromium web browser failed to obtain page source of the URI: {uri}"));
                    return false;
                }
                catch (OperationCanceledException operationCanceledException)
                {
                    if (operationCanceledException.CancellationToken != cancellationToken) throw;
                    onFailed.Invoke(new OperationCanceledException(renderingFailedErrorMessage, cancellationToken));
                    return false;
                }
                finally { _stopwatch.Reset(); }

                bool TryGetPageSource(out string pageSource)
                {
                    try { pageSource = _chromeDriver.PageSource; }
                    catch (WebDriverException webDriverException) when (TimeoutExceptionOccurred(webDriverException))
                    {
                        pageSource = null;
                        return false;
                    }
                    return true;
                }
                bool TryGoToUri(int attemptCount)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EnableNetwork();
                    for (var attemptNo = 0; attemptNo < attemptCount; attemptNo++)
                    {
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            _stopwatch.Start();
                            _chromeDriver.Navigate().GoToUrl(uri);
                            _stopwatch.Stop();
                            break;
                        }
                        catch (WebDriverException webDriverException) when (TimeoutExceptionOccurred(webDriverException))
                        {
                            _stopwatch.Stop();
                            Restart();
                            if (attemptNo < attemptCount) continue;
                            return false;
                        }
                    }
                    DisableNetwork();
                    return true;

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
                }
            }
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
                if (webDriverException.InnerException?.GetType() != typeof(WebException)) throw;
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
            if (workingDirectory == null) throw new InvalidOperationException();

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
            if (_useIncognitoWebBrowser) chromeOptions.AddArguments("--incognito");
            if (_useHeadlessWebBrowser) chromeOptions.AddArguments("--headless");
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
            _httpProxyServer.BeforeRequest += BeforeRequest;
            _httpProxyServer.BeforeResponse += BeforeResponse;
        }

        static bool TimeoutExceptionOccurred(Exception exception)
        {
            return exception is WebDriverTimeoutException ||
                   exception.InnerException is WebException webException && webException.Status == WebExceptionStatus.Timeout;
        }

        ~ChromiumWebBrowser() { ReleaseUnmanagedResources(); }
    }
}