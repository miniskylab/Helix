﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Helix.Core;
using Helix.WebBrowser.Abstractions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using WebBrowser.Abstractions.Enum;

namespace Helix.WebBrowser
{
    public class ChromiumWebBrowser : IWebBrowser
    {
        readonly (int width, int height) _browserWindowSize;
        ChromeDriver _chromeDriver;
        ChromeDriverService _chromeDriverService;
        readonly TimeSpan _commandTimeout;
        ProxyServer _httpProxyServer;
        readonly Stopwatch _pageLoadTimeStopwatch;
        readonly string _pathToChromeDriverExecutable;
        readonly string _pathToChromiumExecutable;
        StateMachine<WebBrowserState, WebBrowserCommand> _stateMachine;
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
            _pageLoadTimeStopwatch = new Stopwatch();
            _pathToChromiumExecutable = pathToChromiumExecutable;
            _pathToChromeDriverExecutable = pathToChromeDriverExecutable;
            _browserWindowSize = browserWindowSize == default ? (1024, 630) : browserWindowSize;
            _useIncognitoWebBrowser = useIncognitoWebBrowser;
            _useHeadlessWebBrowser = useHeadlessWebBrowser;
            _commandTimeout = TimeSpan.FromSeconds(commandTimeoutInSecond);

            SetupStateMachine();
            SetupHttpProxyServer();
            GetUserAgentString();
            OpenWebBrowser(StartArguments);
        }

        public void Dispose()
        {
            if (!_stateMachine.TryMoveNext(WebBrowserCommand.Dispose)) return;
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
            _stateMachine.TryMoveNext(WebBrowserCommand.TransitToDisposedState);
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

        public bool TryRender(Uri uri, out string html, out long? millisecondsPageLoadTime, CancellationToken cancellationToken,
            Action<Exception> onFailed)
        {
            html = null;
            millisecondsPageLoadTime = null;
            if (!_stateMachine.TryMoveNext(WebBrowserCommand.TryRender)) return false;

            var renderingFinishedCts = new CancellationTokenSource();
            try
            {
                CurrentUri = uri ?? throw new ArgumentNullException(nameof(uri));

                EnsureCancellable();
                if (!TryGoToUri(out var failureReason))
                {
                    onFailed?.Invoke(
                        new Exception($"Chromium web browser failed to render the URI: {uri}\r\n-----> {failureReason}")
                    );
                    return false;
                }

                if (!TryGetPageSource(out html, out failureReason))
                {
                    onFailed?.Invoke(
                        new Exception($"Chromium web browser failed to obtain HTML of the URI: {uri}\r\n-----> {failureReason}")
                    );
                    return false;
                }

                millisecondsPageLoadTime = _pageLoadTimeStopwatch.ElapsedMilliseconds;
                return true;
            }
            finally
            {
                _pageLoadTimeStopwatch.Reset();
                renderingFinishedCts.Cancel();
                renderingFinishedCts.Dispose();
                _stateMachine.TryMoveNext(WebBrowserCommand.TransitToIdleState);
            }

            void EnsureCancellable()
            {
                Task.Run(() =>
                {
                    while (true)
                    {
                        if (renderingFinishedCts.IsCancellationRequested) break;
                        if (cancellationToken.IsCancellationRequested)
                        {
                            RestartWebBrowser(true);
                            break;
                        }
                        Thread.Sleep(1000);
                    }
                }, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, renderingFinishedCts.Token).Token);
            }
            bool TryGoToUri(out string failureReason)
            {
                try
                {
                    failureReason = string.Empty;
                    _httpProxyServer.BeforeResponse += BeforeResponse;
                    _httpProxyServer.BeforeRequest += BeforeRequest;
                    _pageLoadTimeStopwatch.Restart();
                    _chromeDriver.Navigate().GoToUrl(uri);
                    return true;
                }
                catch (WebDriverException webDriverException) when (TimeoutExceptionOccurred(webDriverException))
                {
                    RestartWebBrowser(true);
                    failureReason = "Chromium web browser waited too long for a response.";
                    return false;
                }
                catch (WebDriverException webDriverException) when (WebBrowserUnreachable(webDriverException))
                {
                    failureReason = "Chromium web browser was forcibly closed.";
                    return false;
                }
                catch (Exception exception)
                {
                    failureReason = $"{exception}";
                    return false;
                }
                finally
                {
                    _pageLoadTimeStopwatch.Stop();
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
            bool TryGetPageSource(out string pageSource, out string failureReason)
            {
                try
                {
                    failureReason = string.Empty;
                    pageSource = _chromeDriver.PageSource;
                    return true;
                }
                catch (WebDriverException webDriverException) when (TimeoutExceptionOccurred(webDriverException))
                {
                    pageSource = null;
                    failureReason = "Chromium web browser waited too long for a response.";
                    return false;
                }
                catch (WebDriverException webDriverException) when (WebBrowserUnreachable(webDriverException))
                {
                    pageSource = null;
                    failureReason = "Chromium web browser was forcibly closed.";
                    return false;
                }
                catch (Exception exception)
                {
                    pageSource = null;
                    failureReason = $"{exception}";
                    return false;
                }
            }
        }

        public bool TryTakeScreenshot(string pathToScreenshotFile, Action<Exception> onFailed)
        {
            if (!_stateMachine.TryMoveNext(WebBrowserCommand.TryTakeScreenshot)) return false;
            try
            {
                if (CurrentUri == null) throw new InvalidOperationException();
                var pathToDirectoryContainsScreenshotFile = Directory.GetParent(pathToScreenshotFile);
                if (!pathToDirectoryContainsScreenshotFile.Exists) pathToDirectoryContainsScreenshotFile.Create();

                var screenShot = _chromeDriver.GetScreenshot();
                Task.Run(() => { screenShot.SaveAsFile(pathToScreenshotFile, ScreenshotImageFormat.Png); });
                return true;
            }
            catch (WebDriverException webDriverException) when (TimeoutExceptionOccurred(webDriverException))
            {
                RestartWebBrowser(true);
                onFailed?.Invoke(new WebDriverException("Chromium web browser waited too long for a response."));
                return false;
            }
            catch (WebDriverException webDriverException) when (WebBrowserUnreachable(webDriverException))
            {
                onFailed?.Invoke(new WebDriverException("Chromium web browser was forcibly closed."));
                return false;
            }
            catch (Exception exception)
            {
                onFailed?.Invoke(exception);
                return false;
            }
            finally
            {
                _stateMachine.TryMoveNext(WebBrowserCommand.TransitToIdleState);
            }
        }

        void CloseWebBrowser(bool forcibly = false)
        {
            if (!_stateMachine.TryMoveNext(WebBrowserCommand.Close)) return;
            if (forcibly) KillAllRelatedProcesses();
            else
            {
                try { _chromeDriver.Quit(); }
                catch (WebDriverException webDriverException)
                {
                    var chromiumBrowserDoesNotRespondToCommand = webDriverException.InnerException?.GetType() == typeof(WebException);
                    if (chromiumBrowserDoesNotRespondToCommand) KillAllRelatedProcesses();
                    else throw;
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
            if (!_stateMachine.TryMoveNext(WebBrowserCommand.Open)) return;
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
            _stateMachine.TryMoveNext(WebBrowserCommand.TransitToIdleState);
        }

        void ReleaseUnmanagedResources()
        {
            _pageLoadTimeStopwatch?.Stop();
            _httpProxyServer?.Stop();
            _httpProxyServer?.Dispose();
            CloseWebBrowser();
        }

        void RestartWebBrowser(bool forcibly = false)
        {
            CloseWebBrowser(forcibly);
            OpenWebBrowser(StartArguments);
        }

        void SetupHttpProxyServer()
        {
            if (_httpProxyServer != null) return;
            _httpProxyServer = new ProxyServer();
            _httpProxyServer.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Loopback, 0));
            _httpProxyServer.Start();
        }

        void SetupStateMachine()
        {
            _stateMachine = new StateMachine<WebBrowserState, WebBrowserCommand>
            (
                new Dictionary<Transition<WebBrowserState, WebBrowserCommand>, WebBrowserState>
                {
                    { Transition(WebBrowserState.WaitingForOpening, WebBrowserCommand.Open), WebBrowserState.Opening },
                    { Transition(WebBrowserState.Opening, WebBrowserCommand.TransitToIdleState), WebBrowserState.Idle },
                    { Transition(WebBrowserState.Idle, WebBrowserCommand.TryRender), WebBrowserState.TryRendering },
                    { Transition(WebBrowserState.Idle, WebBrowserCommand.TryTakeScreenshot), WebBrowserState.TryTakingScreenshot },
                    { Transition(WebBrowserState.Idle, WebBrowserCommand.Dispose), WebBrowserState.Disposing },
                    { Transition(WebBrowserState.Idle, WebBrowserCommand.Close), WebBrowserState.Closing },
                    { Transition(WebBrowserState.TryRendering, WebBrowserCommand.Close), WebBrowserState.Closing },
                    { Transition(WebBrowserState.TryRendering, WebBrowserCommand.TransitToIdleState), WebBrowserState.Idle },
                    { Transition(WebBrowserState.TryTakingScreenshot, WebBrowserCommand.Close), WebBrowserState.Closing },
                    { Transition(WebBrowserState.TryTakingScreenshot, WebBrowserCommand.TransitToIdleState), WebBrowserState.Idle },
                    { Transition(WebBrowserState.Disposing, WebBrowserCommand.Close), WebBrowserState.Closing },
                    { Transition(WebBrowserState.Closing, WebBrowserCommand.TransitToDisposedState), WebBrowserState.Disposed },
                    { Transition(WebBrowserState.Closing, WebBrowserCommand.Open), WebBrowserState.Opening }
                },
                WebBrowserState.WaitingForOpening
            );

            Transition<WebBrowserState, WebBrowserCommand> Transition(WebBrowserState fromState, WebBrowserCommand command)
            {
                return new Transition<WebBrowserState, WebBrowserCommand>(fromState, command);
            }
        }

        static bool TimeoutExceptionOccurred(Exception exception)
        {
            return exception is WebDriverTimeoutException ||
                   exception.InnerException is WebException webException && webException.Status == WebExceptionStatus.Timeout;
        }

        static bool WebBrowserUnreachable(Exception exception)
        {
            return exception is WebDriverException webDriverException &&
                   webDriverException.InnerException is WebException webException && webException.InnerException is HttpRequestException;
        }

        ~ChromiumWebBrowser() { ReleaseUnmanagedResources(); }
    }
}