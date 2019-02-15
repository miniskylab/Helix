namespace Helix.WebBrowser.Abstractions
{
    public interface IWebBrowserProvider
    {
        IWebBrowser GetWebBrowser(string pathToChromiumExecutable, string pathToChromeDriverExecutable, bool useIncognitoWebBrowser = false,
            bool useHeadlessWebBrowser = true, (int width, int height) browserWindowSize = default);
    }
}