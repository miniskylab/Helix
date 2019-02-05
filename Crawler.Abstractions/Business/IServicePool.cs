using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IServicePool : IDisposable
    {
        void EnsureEnoughResources(CancellationToken cancellationToken);

        IRawResourceExtractor GetRawResourceExtractor(CancellationToken cancellationToken);

        IRawResourceVerifier GetResourceVerifier(CancellationToken cancellationToken);

        IWebBrowser GetWebBrowser(CancellationToken cancellationToken);

        void Return(IRawResourceExtractor rawResourceExtractor);

        void Return(IRawResourceVerifier rawResourceVerifier);

        void Return(IWebBrowser webBrowser);
    }
}