using System;

namespace Helix.Crawler.Abstractions
{
    public interface IWebBrowser : IDisposable
    {
        event Action<Exception> OnExceptionOccurred;
        event IdleEvent OnIdle;
        event Action<RawResource> OnRawResourceCaptured;

        bool TryRender(Uri uri, out string html);
    }
}