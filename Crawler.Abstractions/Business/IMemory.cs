using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IMemory : IDisposable
    {
        Configurations Configurations { get; }

        int ToBeExtractedHtmlDocumentCount { get; }

        int ToBeRenderedResourceCount { get; }

        int ToBeVerifiedResourceCount { get; }

        void Clear();

        void MemorizeToBeExtractedHtmlDocument(HtmlDocument toBeExtractedHtmlDocument, CancellationToken cancellationToken);

        void MemorizeToBeRenderedResource(Resource toBeRenderedResource, CancellationToken cancellationToken);

        void MemorizeToBeVerifiedResource(Resource toBeVerifiedResource, CancellationToken cancellationToken);

        bool TryTakeToBeExtractedHtmlDocument(out HtmlDocument toBeExtractedHtmlDocument);

        bool TryTakeToBeRenderedResource(out Resource toBeRenderedResource);

        bool TryTakeToBeVerifiedResource(out Resource toBeVerifiedResource);
    }
}