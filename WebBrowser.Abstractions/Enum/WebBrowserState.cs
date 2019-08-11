namespace WebBrowser.Abstractions.Enum
{
    public enum WebBrowserState
    {
        WaitingForWebBrowserOpening,
        OpeningWebBrowser,
        Idle,
        TryRendering,
        TryTakingScreenshot,
        ClosingWebBrowser,
        Disposing,
        Disposed
    }
}