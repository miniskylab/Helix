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

        void CancelEverything();

        void EnsureEnoughResources();

        void InterlockedCoordinate(out IRawResourceExtractor rawResourceExtractor, out HtmlDocument toBeExtractedHtmlDocument);

        void InterlockedCoordinate(out IWebBrowser webBrowser, out Uri toBeRenderedUri);

        void InterlockedCoordinate(out IRawResourceVerifier rawResourceVerifier, out RawResource toBeVerifiedRawResource);

        void OnRawResourceExtractionTaskCompleted(IRawResourceExtractor rawResourceExtractor = null,
            HtmlDocument toBeExtractedHtmlDocument = null);

        void OnRawResourceVerificationTaskCompleted(IRawResourceVerifier rawResourceVerifier = null,
            RawResource toBeVerifiedRawResource = null);

        void OnUriRenderingTaskCompleted(IWebBrowser webBrowser = null, Uri toBeRenderedUri = null);

        bool TryTransitTo(CrawlerState crawlerState);
    }
}