using System;
using System.Collections.Generic;
using System.Threading;

namespace Helix.Bot.Abstractions
{
    public interface IHtmlRenderer : IDisposable
    {
        bool TryRender(Resource resource, out string html, out long? millisecondsPageLoadTime, out List<Resource> capturedResources,
            CancellationToken cancellationToken);
    }
}