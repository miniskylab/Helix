namespace Helix.WebBrowser.Abstractions
{
    public enum WebBrowserState
    {
        WaitingForOpening,
        Opening,
        Idle,
        TryRendering,
        TryTakingScreenshot,
        Closing,
        Disposing,
        Disposed
    }
}