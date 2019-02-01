using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IManagement : IDisposable
    {
        CancellationToken CancellationToken { get; }

        CrawlerState CrawlerState { get; }

        bool EverythingIsDone { get; }

        int RemainingUrlCount { get; }

        event Action<string> OnOrphanedResourcesDetected;

        void CancelEverything();

        void EnsureResources();

        void InterlockedCoordinate(out IRawResourceExtractor rawResourceExtractor, out HtmlDocument toBeExtractedHtmlDocument);

        void InterlockedCoordinate(out IWebBrowser webBrowser, out Uri toBeRenderedUri);

        void InterlockedCoordinate(out IRawResourceVerifier rawResourceVerifier, out RawResource toBeVerifiedRawResource);

        void OnRawResourceExtractionTaskCompleted();

        void OnRawResourceVerificationTaskCompleted();

        void OnUriRenderingTaskCompleted();

        bool TryTransitTo(CrawlerState crawlerState);
    }
}