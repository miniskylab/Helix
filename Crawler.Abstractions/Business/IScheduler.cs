using System;
using System.Threading;

namespace Helix.Crawler.Abstractions
{
    public interface IScheduler : IDisposable
    {
        CancellationToken CancellationToken { get; }

        bool EverythingIsDone { get; }

        int RemainingUrlCount { get; }

        void CancelEverything();

        void CreateTask(Action<IRawResourceExtractor, HtmlDocument> taskDescription);

        void CreateTask(Action<IHtmlRenderer, Resource> taskDescription);

        void CreateTask(Action<IRawResourceVerifier, RawResource> taskDescription);
    }
}