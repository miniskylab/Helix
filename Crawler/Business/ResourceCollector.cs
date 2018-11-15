using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Helix.Abstractions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace Helix.Crawler
{
    sealed class ResourceCollector : IResourceCollector
    {
        const int HttpProxyPort = 18882;
        readonly ChromeDriver _chromeDriver;
        static ProxyServer _httpProxyServer;

        public event AllAttemptsToCollectNewRawResourcesFailedEvent OnAllAttemptsToCollectNewRawResourcesFailed;
        public event ExceptionOccurredEvent OnExceptionOccurred;
        public event IdleEvent OnIdle;
        public event NetworkTrafficCapturedEvent OnNetworkTrafficCaptured;
        public event RawResourceCollectedEvent OnRawResourceCollected;

        public ResourceCollector(IConfigurations configurations)
        {
            SetupHttpProxyServer();
            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var chromeDriverService = ChromeDriverService.CreateDefaultService(workingDirectory);
            chromeDriverService.HideCommandPromptWindow = true;

            var chromeOptions = new ChromeOptions();
            if (!configurations.ShowWebBrowsers) chromeOptions.AddArgument("--headless");
            chromeOptions.Proxy = new Proxy
            {
                HttpProxy = $"http://localhost:{HttpProxyPort}",
                SslProxy = $"http://localhost:{HttpProxyPort}",
                FtpProxy = $"http://localhost:{HttpProxyPort}"
            };

            _chromeDriver = new ChromeDriver(chromeDriverService, chromeOptions);
        }

        public void CollectNewRawResourcesFrom(IResource parentResource)
        {
            try
            {
                _chromeDriver.Navigate().GoToUrl(parentResource.Uri);
                var newRawResources = TryGetUrls("a", "href")
                    .Select(url => url.ToLower())
                    .Where(url => url.StartsWith("http") || url.StartsWith("https") || url.StartsWith("/"))
                    .Select(url => new RawResource { Url = url, ParentUrl = parentResource.Uri.AbsoluteUri });
                foreach (var newRawResource in newRawResources) OnRawResourceCollected?.Invoke(newRawResource);
            }
            catch (WebDriverException webDriverException)
            {
                OnExceptionOccurred?.Invoke(webDriverException, parentResource);
            }
            catch (TaskCanceledException)
            {
                OnAllAttemptsToCollectNewRawResourcesFailed?.Invoke(parentResource);
            }
            finally
            {
                OnIdle?.Invoke();
            }
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        async Task CaptureNetworkTraffic(object _, SessionEventArgsBase networkTraffic)
        {
            await Task.Run(() => { OnNetworkTrafficCaptured?.Invoke(networkTraffic); });
        }

        void ReleaseUnmanagedResources()
        {
            _chromeDriver?.Quit();
            _httpProxyServer?.Stop();
            _httpProxyServer?.Dispose();
            _httpProxyServer = null;
        }

        void SetupHttpProxyServer()
        {
            if (_httpProxyServer != null) return;
            var explicitProxyEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, HttpProxyPort);
            _httpProxyServer = new ProxyServer();
            _httpProxyServer.AddEndPoint(explicitProxyEndPoint);
            _httpProxyServer.Start();
            _httpProxyServer.SetAsSystemHttpProxy(explicitProxyEndPoint);
            _httpProxyServer.SetAsSystemHttpsProxy(explicitProxyEndPoint);
            _httpProxyServer.BeforeResponse += CaptureNetworkTraffic;
        }

        IEnumerable<string> TryGetUrls(string tagName, string attributeName)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            while (stopWatch.Elapsed.TotalSeconds < 30)
            {
                try
                {
                    var urls = new List<string>();
                    foreach (var webElement in _chromeDriver.FindElementsByTagName(tagName))
                    {
                        var url = webElement.GetAttribute(attributeName);
                        if (!string.IsNullOrWhiteSpace(url)) urls.Add(url);
                    }
                    return urls;
                }
                catch (StaleElementReferenceException) { }
                Thread.Sleep(1000);
            }

            stopWatch.Reset();
            throw new TaskCanceledException();
        }

        ~ResourceCollector() { ReleaseUnmanagedResources(); }
    }
}