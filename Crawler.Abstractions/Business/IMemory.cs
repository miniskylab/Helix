using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IMemory
    {
        Configurations Configurations { get; }

        int ToBeExtractedHtmlDocumentCount { get; }

        int ToBeRenderedUriCount { get; }

        int ToBeVerifiedRawResourceCount { get; }

        void Clear();

        void Memorize(RawResource toBeVerifiedRawResource, CancellationToken cancellationToken);

        void Memorize(Uri toBeRenderedUri, CancellationToken cancellationToken);

        void Memorize(HtmlDocument toBeExtractedHtmlDocument, CancellationToken cancellationToken);

        bool TryTakeToBeExtractedHtmlDocument(out HtmlDocument htmlDocument);

        bool TryTakeToBeRenderedUri(out Uri uri);

        bool TryTakeToBeVerifiedRawResource(out RawResource rawResource);
    }
}