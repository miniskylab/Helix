using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class Memory : IMemory
    {
        readonly HashSet<string> _alreadyVerifiedUrls;
        readonly object _memorizationLock;
        bool _objectDisposed;
        readonly BlockingCollection<HtmlDocument> _toBeExtractedHtmlDocuments;
        readonly BlockingCollection<Resource> _toBeRenderedResources;
        readonly BlockingCollection<Resource> _toBeTakenScreenshotResources;
        readonly BlockingCollection<RawResource> _toBeVerifiedRawResources;

        public Configurations Configurations { get; }

        public int ToBeExtractedHtmlDocumentCount => _toBeExtractedHtmlDocuments.Count;

        public int ToBeRenderedResourceCount => _toBeRenderedResources.Count + _toBeTakenScreenshotResources.Count;

        public int ToBeVerifiedRawResourceCount => _toBeVerifiedRawResources.Count;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Memory(Configurations configurations)
        {
            Configurations = configurations;
            _objectDisposed = false;
            _memorizationLock = new object();
            _toBeExtractedHtmlDocuments = new BlockingCollection<HtmlDocument>();
            _toBeRenderedResources = new BlockingCollection<Resource>();
            _toBeTakenScreenshotResources = new BlockingCollection<Resource>();
            _alreadyVerifiedUrls = new HashSet<string>();
            _toBeVerifiedRawResources = new BlockingCollection<RawResource>();
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
            var destinationCollection = (int) toBeRenderedResource.HttpStatusCode >= 400
                ? _toBeTakenScreenshotResources
                : _toBeRenderedResources;
            while (!cancellationToken.IsCancellationRequested && !destinationCollection.TryAdd(toBeRenderedResource))
                Thread.Sleep(TimeSpan.FromSeconds(3));
        }

        public void Memorize(HtmlDocument toBeExtractedHtmlDocument, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_toBeExtractedHtmlDocuments.TryAdd(toBeExtractedHtmlDocument))
                Thread.Sleep(TimeSpan.FromSeconds(3));
        }

        public bool TryTake(out HtmlDocument htmlDocument) { return _toBeExtractedHtmlDocuments.TryTake(out htmlDocument); }

        public bool TryTake(out Resource resource)
        {
            return _toBeTakenScreenshotResources.TryTake(out resource) || _toBeRenderedResources.TryTake(out resource);
        }

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