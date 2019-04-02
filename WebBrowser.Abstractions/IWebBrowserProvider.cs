namespace Helix.WebBrowser.Abstractions
{
    public interface IWebBrowserProvider
    {
        IWebBrowser GetWebBrowser(string pathToChromiumExecutable, string pathToChromeDriverExecutable, double commandTimeoutInSecond = 60,
            bool useIncognitoWebBrowser = false, bool useHeadlessWebBrowser = true, (int width, int height) browserWindowSize = default);
    }
}