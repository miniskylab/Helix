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

        HtmlDocument InterlockedTakeToBeExtractedHtmlDocument();

        Uri InterlockedTakeToBeRenderedUri();

        RawResource InterlockedTakeToBeVerifiedRawResource();

        void OnRawResourceExtractionTaskCompleted();

        void OnRawResourceVerificationTaskCompleted();

        void OnUriRenderingTaskCompleted();

        bool TryTransitTo(CrawlerState crawlerState);
    }
}