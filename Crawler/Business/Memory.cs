using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class Memory : IMemory
    {
        readonly ConcurrentSet<string> _alreadyVerifiedUrls;
        readonly object _memorizationLock;
        bool _objectDisposed;
        readonly BlockingCollection<HtmlDocument> _toBeExtractedHtmlDocuments;
        readonly BlockingCollection<Resource> _toBeRenderedResources;
        readonly BlockingCollection<RawResource> _toBeVerifiedRawResources;

        public Configurations Configurations { get; }

        public int ToBeExtractedHtmlDocumentCount => _toBeExtractedHtmlDocuments.Count;

        public int ToBeRenderedResourceCount => _toBeRenderedResources.Count;

        public int ToBeVerifiedRawResourceCount => _toBeVerifiedRawResources.Count;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Memory(Configurations configurations)
        {
            Configurations = configurations;
            _objectDisposed = false;
            _memorizationLock = new object();
            _toBeExtractedHtmlDocuments = new BlockingCollection<HtmlDocument>();
            _toBeRenderedResources = new BlockingCollection<Resource>();
            _alreadyVerifiedUrls = new ConcurrentSet<string> { Configurations.StartUri.AbsoluteUri };
            _toBeVerifiedRawResources = new BlockingCollection<RawResource>
            {
                new RawResource { ParentUri = null, Url = Configurations.StartUri.AbsoluteUri }
            };
        }

        public void Clear()
        {
            lock (_memorizationLock)
            {
                _alreadyVerifiedUrls.Clear();
                while (_toBeVerifiedRawResources.Any()) _toBeVerifiedRawResources.Take();
            }
            while (_toBeExtractedHtmlDocuments.Any()) _toBeExtractedHtmlDocuments.Take();
            while (_toBeRenderedResources.Any()) _toBeRenderedResources.Take();
        }

        public void Dispose()
        {
            lock (_memorizationLock)
            {
                if (_objectDisposed) return;
                ReleaseUnmanagedResources();
                GC.SuppressFinalize(this);
                _objectDisposed = true;
            }
        }

        public void Memorize(RawResource toBeVerifiedRawResource, CancellationToken cancellationToken)
        {
            lock (_memorizationLock)
            {
                if (_alreadyVerifiedUrls.Contains(toBeVerifiedRawResource.Url.StripFragment())) return;
                _alreadyVerifiedUrls.Add(toBeVerifiedRawResource.Url.StripFragment());
            }

            while (!cancellationToken.IsCancellationRequested && !_toBeVerifiedRawResources.TryAdd(toBeVerifiedRawResource))
                Thread.Sleep(TimeSpan.FromSeconds(3));
        }

        public void Memorize(Resource toBeRenderedResource, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_toBeRenderedResources.TryAdd(toBeRenderedResource))
                Thread.Sleep(TimeSpan.FromSeconds(3));
        }

        public void Memorize(HtmlDocument toBeExtractedHtmlDocument, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_toBeExtractedHtmlDocuments.TryAdd(toBeExtractedHtmlDocument))
                Thread.Sleep(TimeSpan.FromSeconds(3));
        }

        public bool TryTake(out HtmlDocument htmlDocument) { return _toBeExtractedHtmlDocuments.TryTake(out htmlDocument); }

        public bool TryTake(out Resource resource) { return _toBeRenderedResources.TryTake(out resource); }

        public bool TryTake(out RawResource rawResource) { return _toBeVerifiedRawResources.TryTake(out rawResource); }

        void ReleaseUnmanagedResources()
        {
            _toBeExtractedHtmlDocuments?.Dispose();
            _toBeRenderedResources?.Dispose();
            _toBeVerifiedRawResources?.Dispose();
        }

        ~Memory() { ReleaseUnmanagedResources(); }
    }
}