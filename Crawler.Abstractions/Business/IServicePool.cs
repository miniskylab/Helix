using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IServicePool : IDisposable
    {
        IHtmlRenderer GetHtmlRenderer(CancellationToken cancellationToken);

        IResourceExtractor GetResourceExtractor(CancellationToken cancellationToken);

        IResourceVerifier GetResourceVerifier(CancellationToken cancellationToken);

        void PreCreateServices(CancellationToken cancellationToken);

        void Return(IResourceExtractor resourceExtractor);

        void Return(IResourceVerifier resourceVerifier);

        void Return(IHtmlRenderer htmlRenderer);
    }
}