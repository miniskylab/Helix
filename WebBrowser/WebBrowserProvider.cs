using Helix.WebBrowser.Abstractions;

namespace Helix.WebBrowser
{
    public class WebBrowserProvider : IWebBrowserProvider
    {
        public IWebBrowser GetWebBrowser(string pathToChromiumExecutable, string pathToChromeDriverExecutable,
            double commandTimeoutInSecond = 60, bool useIncognitoWebBrowser = false, bool useHeadlessWebBrowser = true,
            (int width, int height) browserWindowSize = default)
        {
            return new ChromiumWebBrowser(pathToChromiumExecutable, pathToChromeDriverExecutable, commandTimeoutInSecond,
                useIncognitoWebBrowser, useHeadlessWebBrowser, browserWindowSize);
        }
    }
}