using System;

namespace Helix.Crawler.Abstractions
{
    public interface IWebBrowser : IDisposable
    {
        event IdleEvent OnIdle;

        string Render(Uri uri);
    }
}