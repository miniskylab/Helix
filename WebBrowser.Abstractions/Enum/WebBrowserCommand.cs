namespace WebBrowser.Abstractions.Enum
{
    public enum WebBrowserCommand
    {
        OpenWebBrowser,
        TransitToIdleState,
        TryRender,
        TryTakingScreenshot,
        CloseWebBrowser,
        Dispose,
        TransitToDisposedState
    }
}