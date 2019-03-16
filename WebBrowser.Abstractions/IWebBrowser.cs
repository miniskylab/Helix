using System;
using System.Threading;
using Titanium.Web.Proxy.EventArguments;

namespace Helix.WebBrowser.Abstractions
{
    public interface IWebBrowser : IDisposable
    {
        Uri CurrentUri { get; }

        event AsyncEventHandler<SessionEventArgs> BeforeRequest;
        event AsyncEventHandler<SessionEventArgs> BeforeResponse;

        string GetUserAgentString();

        bool TryRender(Uri uri, out string html, out long? pageLoadTime, CancellationToken cancellationToken,
            int attemptCount = 2, Action<Exception> onFailed = null);

        bool TryTakeScreenshot(string pathToScreenshotFile, Action<Exception> onFailed = null);
    }
}