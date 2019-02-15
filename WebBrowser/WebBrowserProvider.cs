using Helix.WebBrowser.Abstractions;

namespace Helix.WebBrowser
{
    public class WebBrowserProvider : IWebBrowserProvider
    {
        public IWebBrowser GetWebBrowser(string pathToChromiumExecutable, string pathToChromeDriverExecutable,
            bool useIncognitoWebBrowser = false, bool useHeadlessWebBrowser = true, (int width, int height) browserWindowSize = default)
        {
            return new ChromiumWebBrowser(pathToChromiumExecutable, pathToChromeDriverExecutable, useIncognitoWebBrowser,
                useHeadlessWebBrowser, browserWindowSize);
        }
    }
}