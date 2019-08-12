namespace WebBrowser.Abstractions.Enum
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