using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        readonly BlockingCollection<Resource> _toBeVerifiedResources;

        public int AlreadyVerifiedUrlCount
        {
            get
            {
                lock (_memorizationLock) return _alreadyVerifiedUrls.Count;
            }
        }

        public int ToBeExtractedHtmlDocumentCount => _toBeExtractedHtmlDocuments.Count;

        public int ToBeRenderedResourceCount => _toBeRenderedResources.Count + _toBeTakenScreenshotResources.Count;

        public int ToBeVerifiedResourceCount => _toBeVerifiedResources.Count;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Memory()
        {
            _objectDisposed = false;
            _memorizationLock = new object();
            _toBeExtractedHtmlDocuments = new BlockingCollection<HtmlDocument>();
            _toBeRenderedResources = new BlockingCollection<Resource>();
            _toBeTakenScreenshotResources = new BlockingCollection<Resource>();
            _alreadyVerifiedUrls = new HashSet<string>();
            _toBeVerifiedResources = new BlockingCollection<Resource>();
        }

        public void Clear()
        {
            lock (_memorizationLock)
            {
                _alreadyVerifiedUrls.Clear();
                while (_toBeVerifiedResources.Any()) _toBeVerifiedResources.Take();
            }
            while (_toBeExtractedHtmlDocuments.Any()) _toBeExtractedHtmlDocuments.Take();
            while (_toBeRenderedResources.Any()) _toBeRenderedResources.Take();
        }

        public void Dispose()
        {
            lock (_memorizationLock)
            {
                if (_objectDisposed) return;
                _toBeExtractedHtmlDocuments?.Dispose();
                _toBeRenderedResources?.Dispose();
                _toBeTakenScreenshotResources?.Dispose();
                _toBeVerifiedResources?.Dispose();
                _objectDisposed = true;
            }
        }

        public void MemorizeToBeExtractedHtmlDocument(HtmlDocument toBeExtractedHtmlDocument)
        {
            _toBeExtractedHtmlDocuments.Add(toBeExtractedHtmlDocument);
        }

        public void MemorizeToBeRenderedResource(Resource toBeRenderedResource)
        {
            var destinationCollection = (int) toBeRenderedResource.StatusCode >= 400
                ? _toBeTakenScreenshotResources
                : _toBeRenderedResources;
            destinationCollection.Add(toBeRenderedResource);
        }

        public void MemorizeToBeVerifiedResource(Resource toBeVerifiedResource)
        {
            lock (_memorizationLock)
            {
                if (_alreadyVerifiedUrls.Contains(toBeVerifiedResource.AbsoluteUrl)) return;
                _alreadyVerifiedUrls.Add(toBeVerifiedResource.AbsoluteUrl);
            }
            _toBeVerifiedResources.Add(toBeVerifiedResource);
        }

        public bool TryTakeToBeExtractedHtmlDocument(out HtmlDocument toBeExtractedHtmlDocument)
        {
            return _toBeExtractedHtmlDocuments.TryTake(out toBeExtractedHtmlDocument);
        }

        public bool TryTakeToBeRenderedResource(out Resource toBeRenderedResource)
        {
            return _toBeTakenScreenshotResources.TryTake(out toBeRenderedResource) ||
                   _toBeRenderedResources.TryTake(out toBeRenderedResource);
        }

        public bool TryTakeToBeVerifiedResource(out Resource toBeVerifiedResource)
        {
            return _toBeVerifiedResources.TryTake(out toBeVerifiedResource);
        }
    }
}