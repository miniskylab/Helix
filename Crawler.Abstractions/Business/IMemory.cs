using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IMemory
    {
        Configurations Configurations { get; }

        string ErrorLogFilePath { get; }

        int ToBeExtractedHtmlDocumentCount { get; }

        int ToBeRenderedUriCount { get; }

        int ToBeVerifiedRawResourceCount { get; }

        void Memorize(RawResource toBeVerifiedRawResource, CancellationToken cancellationToken);

        void Memorize(Uri toBeRenderedUri, CancellationToken cancellationToken);

        void Memorize(HtmlDocument toBeExtractedHtmlDocument, CancellationToken cancellationToken);

        HtmlDocument TakeToBeExtractedHtmlDocument(CancellationToken cancellationToken);

        Uri TakeToBeRenderedUri(CancellationToken cancellationToken);

        RawResource TakeToBeVerifiedRawResource(CancellationToken cancellationToken);
    }
}