using System;
using System.IO;
using System.Net;
using System.Reflection;
using Helix.Crawler.Abstractions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Helix.Crawler
{
    public class ChromiumWebBrowser : IWebBrowser
    {
        ChromeDriver _chromeDriver;

        public event IdleEvent OnIdle;

        public ChromiumWebBrowser(Configurations configurations)
        {
            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var chromeDriverService = ChromeDriverService.CreateDefaultService(workingDirectory);
            chromeDriverService.HideCommandPromptWindow = true;

            var chromeOptions = new ChromeOptions { BinaryLocation = Path.Combine(workingDirectory, "chromium/chrome.exe") };
            if (!configurations.ShowWebBrowsers) chromeOptions.AddArguments("--headless", "--incognito");
            if (configurations.HttpProxyPort > 0)
                chromeOptions.Proxy = new Proxy
                {
                    HttpProxy = $"http://{IPAddress.Loopback}:{configurations.HttpProxyPort}",
                    FtpProxy = $"http://{IPAddress.Loopback}:{configurations.HttpProxyPort}",
                    SslProxy = $"http://{IPAddress.Loopback}:{configurations.HttpProxyPort}"
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
            _chromeDriver?.Quit();
            _chromeDriver = null;
        }

        ~ChromiumWebBrowser() { ReleaseUnmanagedResources(); }
    }
}