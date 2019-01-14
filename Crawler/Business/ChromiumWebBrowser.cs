using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
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
        readonly Configurations _configurations;
        readonly List<int> _processIds;

        public event Action<Exception> OnExceptionOccurred;
        public event IdleEvent OnIdle;

        public ChromiumWebBrowser(Configurations configurations)
        {
            _configurations = configurations;
            _processIds = new List<int>();
            Restart();
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public string Render(Uri uri)
        {
            var timedOut = true;
            for (var attemptCount = 0; attemptCount < 5; attemptCount++)
            {
                try
                {
                    _chromeDriver.Navigate().GoToUrl(uri);
                    timedOut = false;
                    break;
                }
                catch (WebDriverException webDriverException)
                {
                    if (webDriverException.InnerException.GetType() != typeof(WebException)) throw;
                    Restart();
                }
            }

            if (timedOut)
            {
                var timeOutErrorMessage = $"Chromium web browser failed to render the URI: {uri}";
                if (OnExceptionOccurred != null) OnExceptionOccurred.Invoke(new TimeoutException(timeOutErrorMessage));
                else throw new TimeoutException(timeOutErrorMessage);
            }

            OnIdle?.Invoke();
            return _chromeDriver.PageSource;
        }

        void ForceQuit()
        {
            _processIds.ForEach(processId => Process.GetProcessById(processId).Kill());
            _processIds.Clear();
        }

        void ReleaseUnmanagedResources()
        {
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

            var chromeOptions = new ChromeOptions { BinaryLocation = Path.Combine(workingDirectory, "chromium/chrome.exe") };
            if (_configurations.UseIncognitoWebBrowser) chromeOptions.AddArguments("--incognito");
            if (!_configurations.ShowWebBrowsers) chromeOptions.AddArguments("--headless");
            if (_configurations.HttpProxyPort > 0)
                chromeOptions.Proxy = new Proxy
                {
                    HttpProxy = $"http://{IPAddress.Loopback}:{_configurations.HttpProxyPort}",
                    FtpProxy = $"http://{IPAddress.Loopback}:{_configurations.HttpProxyPort}",
                    SslProxy = $"http://{IPAddress.Loopback}:{_configurations.HttpProxyPort}"
                };
            chromeOptions.AddExtension(Path.Combine(workingDirectory, "initiator.crx"));

            _chromeDriver = new ChromeDriver(chromeDriverService, chromeOptions);
            _processIds.Add(chromeDriverService.ProcessId);

            var childProcessQueryString = $"Select * From Win32_Process Where ParentProcessID={chromeDriverService.ProcessId}";
            var managementObjectSearcher = new ManagementObjectSearcher(childProcessQueryString);
            foreach (var managementObject in managementObjectSearcher.Get())
            {
                var processId = Convert.ToInt32(managementObject["ProcessID"]);
                _processIds.Add(processId);
            }
        }

        ~ChromiumWebBrowser() { ReleaseUnmanagedResources(); }
    }
}