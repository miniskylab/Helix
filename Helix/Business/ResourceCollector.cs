using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Helix
{
    public class ResourceCollector
    {
        readonly ChromeDriver _chromeDriver;
        public event AllAttemptsToCollectResourcesFailedEvent OnAllAttemptsToCollectResourcesFailed;
        public event ExceptionOccurredEvent OnExceptionOccurred;
        public event IdleEvent OnIdle;
        public event RawResourceCollectedEvent OnRawResourceCollected;

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

        public void CollectNewRawResourcesFrom(Resource parentResource)
        {
            try
            {
                _chromeDriver.Navigate().GoToUrl(parentResource.Uri);
                var newResources = TryGetUrls("a", "href")
                    .Union(TryGetUrls("link", "href"))
                    .Union(TryGetUrls("script", "src"))
                    .Union(TryGetUrls("img", "src"))
                    .Select(url => url.ToLower())
                    .Where(url => url.StartsWith("http") || url.StartsWith("https") || url.StartsWith("/"))
                    .Select(url => new RawResource { Url = url, ParentUrl = parentResource.Uri.AbsoluteUri });
                foreach (var newResource in newResources) OnRawResourceCollected?.Invoke(newResource);
            }
            catch (WebDriverException webDriverException)
            {
                OnExceptionOccurred?.Invoke(webDriverException, parentResource);
            }
            catch (TaskCanceledException)
            {
                OnAllAttemptsToCollectResourcesFailed?.Invoke(parentResource);
            }
            finally
            {
                OnIdle?.Invoke();
            }
        }

        public void Dispose() { _chromeDriver.Quit(); }

        IEnumerable<string> TryGetUrls(string tagName, string attributeName)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            while (stopWatch.Elapsed.TotalSeconds < 30)
            {
                try
                {
                    var resourceReferences = new List<string>();
                    foreach (var webElement in _chromeDriver.FindElementsByTagName(tagName))
                        resourceReferences.Add(webElement.GetAttribute(attributeName) ?? string.Empty);
                    return resourceReferences;
                }
                catch (StaleElementReferenceException) { }
                Thread.Sleep(1000);
            }

            throw new TaskCanceledException();
        }

        public delegate void AllAttemptsToCollectResourcesFailedEvent(Resource parentResource);
        public delegate void ExceptionOccurredEvent(WebDriverException webDriverException, Resource resourceThatTriggeredThisException);
        public delegate void IdleEvent();
        public delegate void RawResourceCollectedEvent(RawResource rawResource);
    }
}