using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IHtmlRenderer : IDisposable
    {
        event Action<RawResource> OnRawResourceCaptured;

        bool TryRender(Uri uri, Action<Exception> onFailed, CancellationToken cancellationToken, out string html, out long? pageLoadTime,
            int attemptCount = 3);
    }
}