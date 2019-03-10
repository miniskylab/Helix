using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IServicePool : IDisposable
    {
        void EnsureEnoughResources(CancellationToken cancellationToken);

        IHtmlRenderer GetHtmlRenderer(CancellationToken cancellationToken);

        IResourceExtractor GetResourceExtractor(CancellationToken cancellationToken);

        IResourceVerifier GetResourceVerifier(CancellationToken cancellationToken);

        void Return(IResourceExtractor resourceExtractor);

        void Return(IResourceVerifier resourceVerifier);

        void Return(IHtmlRenderer htmlRenderer);
    }
}