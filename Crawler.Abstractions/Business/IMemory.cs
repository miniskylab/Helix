using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IMemory : IDisposable
    {
        Configurations Configurations { get; }

        int ToBeExtractedHtmlDocumentCount { get; }

        int ToBeRenderedResourceCount { get; }

        int ToBeVerifiedRawResourceCount { get; }

        void Clear();

        void Memorize(RawResource toBeVerifiedRawResource, CancellationToken cancellationToken);

        void Memorize(Resource toBeRenderedResource, CancellationToken cancellationToken);

        void Memorize(HtmlDocument toBeExtractedHtmlDocument, CancellationToken cancellationToken);

        bool TryTake(out HtmlDocument htmlDocument);

        bool TryTake(out Resource resource);

        bool TryTake(out RawResource rawResource);
    }
}