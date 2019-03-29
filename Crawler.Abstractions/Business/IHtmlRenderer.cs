using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IHtmlRenderer : IDisposable
    {
        event Action<Resource> OnResourceCaptured;

        bool TryRender(Resource resource, out string html, out long? millisecondsPageLoadTime, CancellationToken cancellationToken,
            Action<Exception> onFailed = null);
    }
}