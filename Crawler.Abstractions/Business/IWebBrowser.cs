using System;

namespace Helix.Crawler.Abstractions
{
    public interface IWebBrowser : IDisposable
    {
        event Action<Exception> OnExceptionOccurred;
        event IdleEvent OnIdle;

        string Render(Uri uri);
    }
}