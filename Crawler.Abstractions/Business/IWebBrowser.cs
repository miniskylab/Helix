using System;

namespace Helix.Crawler.Abstractions
{
    public interface IWebBrowser : IDisposable
    {
        event IdleEvent OnIdle;
        event Action<RawResource> OnRawResourceCaptured;

        bool TryRender(Uri uri, Action<Exception> onFailed, out string html);
    }
}