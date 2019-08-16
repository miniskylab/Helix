namespace Helix.WebBrowser.Abstractions
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