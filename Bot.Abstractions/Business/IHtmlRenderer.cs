using System;
using System.Collections.Generic;
using System.Threading;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IHtmlRenderer : IService, IDisposable
    {
        bool TryRender(Resource resource, out string html, out long? millisecondsPageLoadTime, out List<Resource> capturedResources,
            CancellationToken cancellationToken);
    }
}