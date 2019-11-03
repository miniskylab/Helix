using System;
using System.Threading;

namespace Helix.Bot.Abstractions
{
    public interface IHtmlRenderer : IDisposable
    {
        event Action<Resource> OnResourceCaptured;

        bool TryRender(Resource resource, out string html, out long? millisecondsPageLoadTime, CancellationToken cancellationToken);
    }
}