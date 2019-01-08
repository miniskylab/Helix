using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IMemory
    {
        CancellationToken CancellationToken { get; }

        Configurations Configurations { get; }

        CrawlerState CrawlerState { get; }

        string ErrorFilePath { get; }

        bool EverythingIsDone { get; }

        int RemainingUrlCount { get; }

        void CancelEverything();

        void DecrementActiveThreadCount();

        void IncrementActiveThreadCount();

        void Memorize(RawResource toBeVerifiedRawResource);

        void Memorize(Uri toBeRenderedUri);

        void Memorize(HtmlDocument toBeExtractedHtmlDocument);

        HtmlDocument TakeToBeExtractedHtmlDocument();

        Uri TakeToBeRenderedUri();

        RawResource TakeToBeVerifiedRawResource();

        bool TryTransitTo(CrawlerState crawlerState);
    }
}