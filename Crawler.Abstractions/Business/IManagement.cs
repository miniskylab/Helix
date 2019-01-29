using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IManagement
    {
        CancellationToken CancellationToken { get; }

        CrawlerState CrawlerState { get; }

        bool EverythingIsDone { get; }

        int RemainingUrlCount { get; }

        void CancelEverything();

        void OnRawResourceExtractionTaskCompleted();

        void OnRawResourceVerificationTaskCompleted();

        void OnUriRenderingTaskCompleted();

        HtmlDocument TakeToBeExtractedHtmlDocument();

        Uri TakeToBeRenderedUri();

        RawResource TakeToBeVerifiedRawResource();

        bool TryTransitTo(CrawlerState crawlerState);
    }
}