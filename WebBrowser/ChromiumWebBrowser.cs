using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Text.RegularExpressions;
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

        static string WaitingPage
        {
            get
            {
                const string html = @"data:text/html;charset=utf-8,
                <html>
                    <head>
                        <style>
                            body {
                                display: flex;
                                justify-content: center;
                                align-items: center;
                                flex-direction: column;
                                background-color: rgb(30, 30, 30);
                            }
                            .text {
                                font-family: cursive;
                                font-weight: bold;
                                font-size: 16px;
                                margin-top: 25px;
                                margin-bottom: -15px;
                            }
                            .loading-spinner { height: 60px; }
                            .outer-circle {
                                width: 50px;
                                height: 50px;
                                margin: 0 auto;
                                background-color: rgba(0, 0, 0, 0);
                                border: 5px solid rgba(0, 183, 229, 0.9);
                                border-right: 5px solid rgba(0, 0, 0, 0);
                                border-left: 5px solid rgba(0, 0, 0, 0);
                                border-radius: 50px;
                                box-shadow: 0 0 35px rgb(33, 135, 231);
                                opacity: .9;
                                animation: blink-spin 1s infinite ease-in-out;
                            }
                            .inner-circle {
                                position: relative;
                                width: 30px;
                                height: 30px;
                                margin: 0 auto;
                                top: -50px;
                                background-color: rgba(0, 0, 0, 0);
                                border: 5px solid rgba(0, 183, 229, 0.9);
                                border-left: 5px solid rgba(0, 0, 0, 0);
                                border-right: 5px solid rgba(0, 0, 0, 0);
                                border-radius: 50px;
                                box-shadow: 0 0 15px rgb(33, 135, 231);
                                opacity: .9;
                                animation: spin 1s infinite linear;
                            }
                            @keyframes blink-spin {
                                0% {
                                    transform: rotate(160deg);
                                    box-shadow: 0 0 1px rgb(33, 135, 231);
                                    opacity: 0;
                                }
                                50% {
                                    transform: rotate(145deg);
                                    opacity: 1;
                                }
                                100% {
                                    transform: rotate(-320deg);
                                    opacity: 0;
                                }
                            }
                            @keyframes spin {
                                0% { transform: rotate(0); }
                                100% { transform: rotate(360deg); }
                            }
                        </style>
                    </head>
                    <body>
                        <div class=""loading-spinner"">
                            <div class=""outer-circle""></div>
                            <div class=""inner-circle""></div>
                        </div>
                        <div class=""text"" style=""color: rgb(0, 183, 229);"">Helix is testing web browser ...</div>
                        <div class=""text"" style=""color: rgb(255, 99, 71);"">Please do not close this web browser manually.</div>
                    </body>
                </html>";
                return Regex.Replace(html, "\\s+", " ");
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
            SetupHttpProxyServer();
            GetUserAgentString();
            OpenWebBrowser(StartArguments);
        }

        public void Dispose()
        {
            if (_objectDisposed) return;
            _objectDisposed = true;

            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public string GetUserAgentString()
        {
            if (!string.IsNullOrEmpty(_userAgentString)) return _userAgentString;
            try
            {
                OpenWebBrowser(new[] { "--incognito", $"--start-maximized {WaitingPage}" });
                _userAgentString = (string) _chromeDriver.ExecuteScript("return navigator.userAgent");
            }
            finally { CloseWebBrowser(); }
            return _userAgentString;
        }

        public void Restart(bool forcibly = false)
        {
            CloseWebBrowser(forcibly);
            OpenWebBrowser(StartArguments);
        }

        public bool TryRender(Uri uri, out string html, out long? millisecondsPageLoadTime, Action<Exception> onFailed)
        {
            CurrentUri = uri ?? throw new ArgumentNullException(nameof(uri));

            html = null;
            millisecondsPageLoadTime = null;
            var renderingFailedErrorMessage = $"Chromium web browser failed to render the URI: {uri}";
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ChromiumWebBrowser));

            try
            {
                if (!TryGoToUri())
                {
                    onFailed?.Invoke(new Exception(renderingFailedErrorMessage));
                    return false;
                }

                if (!TryGetPageSource(out html))
                {
                    onFailed?.Invoke(new MemberAccessException($"Chromium web browser failed to obtain page source of the URI: {uri}"));
                    return false;
                }

                millisecondsPageLoadTime = _stopwatch.ElapsedMilliseconds;
                return true;
            }
            finally { _stopwatch.Reset(); }

            bool TryGoToUri()
            {
                try
                {
                    _httpProxyServer.BeforeResponse += BeforeResponse;
                    _httpProxyServer.BeforeRequest += BeforeRequest;
                    _stopwatch.Restart();
                    _chromeDriver.Navigate().GoToUrl(uri);
                    return true;
                }
                catch (NullReferenceException) when (_chromeDriver == null) { return false; }
                catch (WebDriverException webDriverException) when (TimeoutExceptionOccurred(webDriverException))
                {
                    CloseWebBrowser(true);
                    OpenWebBrowser(StartArguments);
                    return false;
                }
                finally
                {
                    _stopwatch.Stop();
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
            bool TryGetPageSource(out string pageSource)
            {
                try { pageSource = _chromeDriver.PageSource; }
                catch (Exception exception) when (exception is NullReferenceException || TimeoutExceptionOccurred(exception))
                {
                    pageSource = null;
                    return false;
                }
                return true;
            }
        }

        public bool TryTakeScreenshot(string pathToScreenshotFile, Action<Exception> onFailed)
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

        void CloseWebBrowser(bool forcibly = false)
        {
            if (forcibly || _chromeDriver == null) KillAllRelatedProcesses();
            else
            {
                try { _chromeDriver.Quit(); }
                catch (WebDriverException webDriverException)
                {
                    if (webDriverException.InnerException?.GetType() != typeof(WebException)) throw;
                    KillAllRelatedProcesses();
                }
            }

            _chromeDriver?.Dispose();
            _chromeDriverService.Dispose();

            _chromeDriver = null;
            _chromeDriverService = null;

            void KillAllRelatedProcesses()
            {
                if (_chromeDriverService == null) return;
                var managementObjectSearcher = new ManagementObjectSearcher(
                    "SELECT ProcessID, ParentProcessID, CreationDate " +
                    "FROM Win32_Process " +
                    $"WHERE ParentProcessID={_chromeDriverService.ProcessId}"
                );
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
            _stopwatch?.Stop();
            _httpProxyServer?.Stop();
            _httpProxyServer?.Dispose();
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