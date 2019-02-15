using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IServicePool : IDisposable
    {
        void EnsureEnoughResources(CancellationToken cancellationToken);

        IHtmlRenderer GetHtmlRenderer(CancellationToken cancellationToken);

        IRawResourceExtractor GetRawResourceExtractor(CancellationToken cancellationToken);

        IRawResourceVerifier GetResourceVerifier(CancellationToken cancellationToken);

        void Return(IRawResourceExtractor rawResourceExtractor);

        void Return(IRawResourceVerifier rawResourceVerifier);

        void Return(IHtmlRenderer htmlRenderer);
    }
}