namespace WebBrowser.Abstractions.Enum
{
    public enum WebBrowserCommand
    {
        Open,
        TransitToIdleState,
        TryRender,
        TryTakeScreenshot,
        Close,
        Dispose,
        TransitToDisposedState
    }
}