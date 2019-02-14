namespace Helix.WebBrowser.Abstractions
{
    public interface IWebBrowserProvider
    {
        IWebBrowser GetWebBrowser(bool useIncognitoWebBrowser, bool useHeadlessWebBrowser);
    }
}