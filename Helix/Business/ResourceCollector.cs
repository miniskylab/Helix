using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Helix
{
    public class ResourceCollector
    {
        readonly ChromeDriver _chromeDriver;
        public event IdleEvent OnIdle;
        public event ResourceCollectedEvent OnResourceCollected;

        public ResourceCollector()
        {
            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var chromeDriverService = ChromeDriverService.CreateDefaultService(workingDirectory);
            chromeDriverService.HideCommandPromptWindow = true;

            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArgument($"--user-agent={Configurations.UserAgent}");
            if (!Configurations.EnableDebugMode) chromeOptions.AddArgument("--headless");

            _chromeDriver = new ChromeDriver(chromeDriverService, chromeOptions);
        }

        public void CollectNewResourcesFrom(Resource parentResource)
        {
            _chromeDriver.Navigate().GoToUrl(parentResource.Uri);
            foreach (var resource in _CollectNewResourcesFrom(parentResource)) OnResourceCollected?.Invoke(resource);
            OnIdle?.Invoke();
        }

        public void Dispose() { _chromeDriver.Quit(); }

        IEnumerable<Resource> _CollectNewResourcesFrom(Resource parentResource)
        {
            try
            {
                return _chromeDriver.FindElementsByTagName("a").Select(anchorTag => anchorTag.GetAttribute("href"))
                    .Union(_chromeDriver.FindElementsByTagName("link").Select(linkTag => linkTag.GetAttribute("href")))
                    .Union(_chromeDriver.FindElementsByTagName("script").Select(scriptTag => scriptTag.GetAttribute("src")))
                    .Union(_chromeDriver.FindElementsByTagName("img").Select(imgTag => imgTag.GetAttribute("src")))
                    .Where(UrlIsValid)
                    .Select(url => new Resource(new Uri(url), parentResource.Uri));
            }
            catch (StaleElementReferenceException)
            {
                return _CollectNewResourcesFrom(parentResource);
            }
        }

        static bool UrlIsValid(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            url = url.ToLower();
            return url.StartsWith("http") || url.StartsWith("https") || url.StartsWith("/");
        }

        public delegate void IdleEvent();
        public delegate void ResourceCollectedEvent(Resource resource);
    }
}