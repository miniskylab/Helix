using System;

namespace Helix.Crawler.Abstractions
{
    public interface IMemory : IDisposable
    {
        int ToBeExtractedHtmlDocumentCount { get; }

        int ToBeRenderedResourceCount { get; }

        int ToBeVerifiedResourceCount { get; }

        void Clear();

        void MemorizeToBeExtractedHtmlDocument(HtmlDocument toBeExtractedHtmlDocument);

        void MemorizeToBeRenderedResource(Resource toBeRenderedResource);

        void MemorizeToBeVerifiedResource(Resource toBeVerifiedResource);

        bool TryTakeToBeExtractedHtmlDocument(out HtmlDocument toBeExtractedHtmlDocument);

        bool TryTakeToBeRenderedResource(out Resource toBeRenderedResource);

        bool TryTakeToBeVerifiedResource(out Resource toBeVerifiedResource);
    }
}