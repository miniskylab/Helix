using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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
            var newResources = TryGetResourceReferences("a", "href")
                .Union(TryGetResourceReferences("link", "href"))
                .Union(TryGetResourceReferences("script", "src"))
                .Union(TryGetResourceReferences("img", "src"))
                .Where(UrlIsValid)
                .Select(url => new Resource(new Uri(url), parentResource.Uri));
            foreach (var newResource in newResources) OnResourceCollected?.Invoke(newResource);
            OnIdle?.Invoke();
        }

        public void Dispose() { _chromeDriver.Quit(); }

        IEnumerable<string> TryGetResourceReferences(string tagName, string attributeName)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            while (stopWatch.Elapsed.TotalSeconds < 30)
            {
                try
                {
                    var resourceReferences = new List<string>();
                    foreach (var webElement in _chromeDriver.FindElementsByTagName(tagName))
                        resourceReferences.Add(webElement.GetAttribute(attributeName));
                    return resourceReferences;
                }
                catch (StaleElementReferenceException) { }
                Thread.Sleep(1000);
            }
            return new List<string>();
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