using System;

namespace Helix.Crawler.Abstractions
{
    public interface IWebBrowser : IDisposable
    {
        event Action OnIdle;
        event Action<RawResource> OnRawResourceCaptured;

        bool TryRender(Uri uri, Action<Exception> onFailed, out string html);
    }
}