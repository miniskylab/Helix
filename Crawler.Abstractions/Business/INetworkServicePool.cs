using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface INetworkServicePool : IDisposable
    {
        IHtmlRenderer GetHtmlRenderer(CancellationToken cancellationToken);

        IResourceExtractor GetResourceExtractor(CancellationToken cancellationToken);

        IResourceVerifier GetResourceVerifier(CancellationToken cancellationToken);

        void Return(IResourceExtractor resourceExtractor);

        void Return(IResourceVerifier resourceVerifier);

        void Return(IHtmlRenderer htmlRenderer);
    }
}