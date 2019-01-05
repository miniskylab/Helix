using System;

namespace Helix.Crawler.Abstractions
{
    public interface IWebBrowser : IDisposable
    {
        string GetPageSource(Uri uri);
    }
}