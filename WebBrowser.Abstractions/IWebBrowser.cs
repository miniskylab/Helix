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

        bool TryRender(Uri uri, Action<Exception> onFailed, CancellationToken cancellationToken, out string html, out long? pageLoadTime,
            int attemptCount = 3);
    }
}