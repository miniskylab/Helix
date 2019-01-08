using System;
using System.IO;
using System.Net;
using System.Reflection;
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
        const int HttpProxyPort = 18882;
        ChromeDriver _chromeDriver;
        static ProxyServer _httpProxyServer;
        static readonly object StaticLock = new object();

        public event AsyncEventHandler<SessionEventArgs> BeforeRequest;
        public event AsyncEventHandler<SessionEventArgs> BeforeResponse;
        public event IdleEvent OnIdle;

        public ChromiumWebBrowser(Configurations configurations)
        {
            SetupHttpProxyServer();

            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var chromeDriverService = ChromeDriverService.CreateDefaultService(workingDirectory);
            chromeDriverService.HideCommandPromptWindow = true;

            var chromeOptions = new ChromeOptions();
            if (!configurations.ShowWebBrowsers) chromeOptions.AddArguments("--headless", "--incognito");
            chromeOptions.BinaryLocation = Path.Combine(workingDirectory, "chromium/chrome.exe");
            chromeOptions.Proxy = new Proxy
            {
                HttpProxy = $"http://localhost:{HttpProxyPort}",
                SslProxy = $"http://localhost:{HttpProxyPort}",
                FtpProxy = $"http://localhost:{HttpProxyPort}"
            };

            _chromeDriver = new ChromeDriver(chromeDriverService, chromeOptions);
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public string Render(Uri uri)
        {
            _chromeDriver.Navigate().GoToUrl(uri);
            OnIdle?.Invoke();
            return _chromeDriver.PageSource;
        }

        void ReleaseUnmanagedResources()
        {
            lock (StaticLock)
            {
                _chromeDriver?.Quit();
                _httpProxyServer?.Stop();
                _httpProxyServer?.Dispose();

                _chromeDriver = null;
                _httpProxyServer = null;
            }
        }

        void SetupHttpProxyServer()
        {
            lock (StaticLock)
            {
                if (_httpProxyServer != null) return;
                _httpProxyServer = new ProxyServer();
                _httpProxyServer.AddEndPoint(new ExplicitProxyEndPoint(IPAddress.Any, HttpProxyPort));
                _httpProxyServer.Start();
                _httpProxyServer.BeforeRequest += BeforeRequest;
                _httpProxyServer.BeforeResponse += BeforeResponse;
            }
        }

        ~ChromiumWebBrowser() { ReleaseUnmanagedResources(); }
    }
}