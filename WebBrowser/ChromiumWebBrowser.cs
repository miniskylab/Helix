using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Helix.WebBrowser.Abstractions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace Helix.WebBrowser
{
    public class ChromiumWebBrowser : IWebBrowser
    {
        readonly (int width, int height) _browserWindowSize;
        ChromeDriver _chromeDriver;
        ChromeDriverService _chromeDriverService;
        readonly TimeSpan _commandTimeout;
        ProxyServer _httpProxyServer;
        bool _objectDisposed;
        readonly string _pathToChromeDriverExecutable;
        readonly string _pathToChromiumExecutable;
        readonly Dictionary<string, object> _publicApiLockMap;
        readonly Stopwatch _stopwatch;
        readonly bool _useHeadlessWebBrowser;
        readonly bool _useIncognitoWebBrowser;
        static string _userAgentString;

        public Uri CurrentUri { get; private set; }

        IEnumerable<string> StartArguments
        {
            get
            {
                var arguments = new List<string>
                {
                    $"--window-size={_browserWindowSize.width},{_browserWindowSize.height}",
                    $"--user-agent={_userAgentString}"
                };
                if (_useIncognitoWebBrowser) arguments.Add("--incognito");
                if (_useHeadlessWebBrowser) arguments.Add("--headless");
                return arguments;
            }
        }

        public event AsyncEventHandler<SessionEventArgs> BeforeRequest;
        public event AsyncEventHandler<SessionEventArgs> BeforeResponse;

        public ChromiumWebBrowser(string pathToChromiumExecutable, string pathToChromeDriverExecutable, double commandTimeoutInSecond = 60,
            bool useIncognitoWebBrowser = false, bool useHeadlessWebBrowser = true, (int width, int height) browserWindowSize = default)
        {
            _objectDisposed = false;
            _stopwatch = new Stopwatch();
            _pathToChromiumExecutable = pathToChromiumExecutable;
            _pathToChromeDriverExecutable = pathToChromeDriverExecutable;
            _browserWindowSize = browserWindowSize == default ? (1024, 630) : browserWindowSize;
            _useIncognitoWebBrowser = useIncognitoWebBrowser;
            _useHeadlessWebBrowser = useHeadlessWebBrowser;
            _commandTimeout = TimeSpan.FromSeconds(commandTimeoutInSecond);
            _publicApiLockMap = new Dictionary<string, object>
            {
                { $"{nameof(TryRender)}", new object() },
                { $"{nameof(TryTakeScreenshot)}", new object() },
                { $"{nameof(GetUserAgentString)}", new object() }
            };
            SetupHttpProxyServer();
            GetUserAgentString();
            OpenWebBrowser(StartArguments);
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

        public string GetUserAgentString()
        {
            lock (_publicApiLockMap[nameof(GetUserAgentString)])
            {
                if (!string.IsNullOrEmpty(_userAgentString)) return _userAgentString;
                OpenWebBrowser(new[] { "--window-position=0,-2000", "--window-size=1,1" });
                _userAgentString = (string) _chromeDriver.ExecuteScript("return navigator.userAgent;");
                CloseWebBrowser();
                return _userAgentString;
            }
        }

        public bool TryRender(Uri uri, out string html, out long? millisecondsPageLoadTime, CancellationToken cancellationToken,
            Action<Exception> onFailed = null)
        {
            CurrentUri = uri ?? throw new ArgumentNullException(nameof(uri));

            html = null;
            millisecondsPageLoadTime = null;
            var renderingFailedErrorMessage = $"Chromium web browser failed to render the URI: {uri}";
            lock (_publicApiLockMap[nameof(TryRender)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ChromiumWebBrowser));
                try
                {
                    if (!TryGoToUri())
                    {
                        onFailed?.Invoke(new TimeoutException(renderingFailedErrorMessage));
                        return false;
                    }

                    if (TryGetPageSource(out html))
                    {
                        millisecondsPageLoadTime = _stopwatch.ElapsedMilliseconds;
                        return true;
                    }
                    onFailed?.Invoke(new MemberAccessException($"Chromium web browser failed to obtain page source of the URI: {uri}"));
                    return false;
                }
                catch (OperationCanceledException operationCanceledException)
                {
                    if (operationCanceledException.CancellationToken != cancellationToken) throw;
                    onFailed?.Invoke(new OperationCanceledException(renderingFailedErrorMessage, cancellationToken));
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
                bool TryGoToUri()
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _httpProxyServer.BeforeResponse += BeforeResponse;
                        _httpProxyServer.BeforeRequest += BeforeRequest;
                        _stopwatch.Restart();
                        _chromeDriver.Navigate().GoToUrl(uri);
                        _stopwatch.Stop();
                        return true;
                    }
                    catch (WebDriverException webDriverException) when (TimeoutExceptionOccurred(webDriverException))
                    {
                        _stopwatch.Stop();
                        CloseWebBrowser(true);
                        OpenWebBrowser(StartArguments);
                        return false;
                    }
                    finally
                    {
                        _httpProxyServer.BeforeRequest -= BeforeRequest;
                        _httpProxyServer.BeforeResponse -= BeforeResponse;
                    }

                    Task BeforeRequest(object sender, SessionEventArgs networkTraffic)
                    {
                        return this.BeforeRequest?.Invoke(sender, networkTraffic);
                    }
                    Task BeforeResponse(object sender, SessionEventArgs networkTraffic)
                    {
                        return this.BeforeResponse?.Invoke(sender, networkTraffic);
                    }
                }
            }
        }

        public bool TryTakeScreenshot(string pathToScreenshotFile, Action<Exception> onFailed = null)
        {
            lock (_publicApiLockMap[nameof(TryTakeScreenshot)])
            {
                try
                {
                    if (CurrentUri == null) throw new InvalidOperationException();
                    var pathToDirectoryContainsScreenshotFile = Directory.GetParent(pathToScreenshotFile);
                    if (!pathToDirectoryContainsScreenshotFile.Exists) pathToDirectoryContainsScreenshotFile.Create();

                    var screenShot = _chromeDriver.GetScreenshot();
                    Task.Run(() => { screenShot.SaveAsFile(pathToScreenshotFile, ScreenshotImageFormat.Png); });
                    return true;
                }
                catch (Exception exception)
                {
                    onFailed?.Invoke(exception);
                    return false;
                }
            }
        }

        void CloseWebBrowser(bool forcibly = false)
        {
            if (forcibly) KillAllRelatedProcesses();
            else
            {
                try { _chromeDriver?.Quit(); }
                catch (WebDriverException webDriverException)
                {
                    if (webDriverException.InnerException?.GetType() != typeof(WebException)) throw;
                    KillAllRelatedProcesses();
                }
            }
            _chromeDriver = null;
            _chromeDriverService = null;

            void KillAllRelatedProcesses()
            {
                var childProcessQueryString = $"Select * From Win32_Process Where ParentProcessID={_chromeDriverService.ProcessId}";
                var managementObjectSearcher = new ManagementObjectSearcher(childProcessQueryString);
                foreach (var managementObject in managementObjectSearcher.Get())
                {
                    var processId = Convert.ToInt32(managementObject["ProcessID"]);
                    Process.GetProcessById(processId).Kill();
                }
                Process.GetProcessById(_chromeDriverService.ProcessId).Kill();
            }
        }

        void OpenWebBrowser(IEnumerable<string> arguments)
        {
            _chromeDriverService = ChromeDriverService.CreateDefaultService(_pathToChromeDriverExecutable);
            _chromeDriverService.HideCommandPromptWindow = true;

            var chromeOptions = new ChromeOptions
            {
                BinaryLocation = _pathToChromiumExecutable,
                Proxy = new Proxy
                {
                    HttpProxy = $"http://{IPAddress.Loopback}:{_httpProxyServer.ProxyEndPoints[0].Port}",
                    FtpProxy = $"http://{IPAddress.Loopback}:{_httpProxyServer.ProxyEndPoints[0].Port}",
                    SslProxy = $"http://{IPAddress.Loopback}:{_httpProxyServer.ProxyEndPoints[0].Port}"
                }
            };
            foreach (var argument in arguments) chromeOptions.AddArguments(argument);
            _chromeDriver = new ChromeDriver(_chromeDriverService, chromeOptions, _commandTimeout);
        }

        void ReleaseUnmanagedResources()
        {
            _httpProxyServer?.Stop();
            _httpProxyServer?.Dispose();
            _httpProxyServer = null;
            CloseWebBrowser();
        }

        void SetupHttpProxyServer()
        {
            if (_httpProxyServer != null) return;
            _httpProxyServer = new ProxyServer();
            _httpProxyServer.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Loopback, 0));
            _httpProxyServer.Start();
        }

        static bool TimeoutExceptionOccurred(Exception exception)
        {
            return exception is WebDriverTimeoutException ||
                   exception.InnerException is WebException webException && webException.Status == WebExceptionStatus.Timeout;
        }

        ~ChromiumWebBrowser() { ReleaseUnmanagedResources(); }
    }
}