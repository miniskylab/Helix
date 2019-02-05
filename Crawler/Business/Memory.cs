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
        readonly object _syncRoot;
        readonly BlockingCollection<HtmlDocument> _toBeExtractedHtmlDocuments;
        readonly BlockingCollection<Uri> _toBeRenderedUris;
        readonly BlockingCollection<RawResource> _toBeVerifiedRawResources;

        public Configurations Configurations { get; }

        public int ToBeExtractedHtmlDocumentCount => _toBeExtractedHtmlDocuments.Count;

        public int ToBeRenderedUriCount => _toBeRenderedUris.Count;

        public int ToBeVerifiedRawResourceCount => _toBeVerifiedRawResources.Count;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Memory(Configurations configurations)
        {
            Configurations = configurations;
            _syncRoot = new object();
            _toBeExtractedHtmlDocuments = new BlockingCollection<HtmlDocument>();
            _toBeRenderedUris = new BlockingCollection<Uri>();
            _alreadyVerifiedUrls = new ConcurrentSet<string> { Configurations.StartUri.AbsoluteUri };
            _toBeVerifiedRawResources = new BlockingCollection<RawResource>
            {
                new RawResource { ParentUri = null, Url = Configurations.StartUri.AbsoluteUri }
            };
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _alreadyVerifiedUrls.Clear();
                while (_toBeVerifiedRawResources.Any()) _toBeVerifiedRawResources.Take();
            }
            while (_toBeExtractedHtmlDocuments.Any()) _toBeExtractedHtmlDocuments.Take();
            while (_toBeRenderedUris.Any()) _toBeRenderedUris.Take();
        }

        public void Memorize(RawResource toBeVerifiedRawResource, CancellationToken cancellationToken)
        {
            lock (_syncRoot)
            {
                if (_alreadyVerifiedUrls.Contains(toBeVerifiedRawResource.Url.StripFragment())) return;
                _alreadyVerifiedUrls.Add(toBeVerifiedRawResource.Url.StripFragment());
            }

            while (!cancellationToken.IsCancellationRequested && !_toBeVerifiedRawResources.TryAdd(toBeVerifiedRawResource))
                Thread.Sleep(TimeSpan.FromSeconds(3));
        }

        public void Memorize(Uri toBeRenderedUri, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_toBeRenderedUris.TryAdd(toBeRenderedUri))
                Thread.Sleep(TimeSpan.FromSeconds(3));
        }

        public void Memorize(HtmlDocument toBeExtractedHtmlDocument, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_toBeExtractedHtmlDocuments.TryAdd(toBeExtractedHtmlDocument))
                Thread.Sleep(TimeSpan.FromSeconds(3));
        }

        public bool TryTake(out HtmlDocument htmlDocument) { return _toBeExtractedHtmlDocuments.TryTake(out htmlDocument); }

        public bool TryTake(out Uri uri) { return _toBeRenderedUris.TryTake(out uri); }

        public bool TryTake(out RawResource rawResource) { return _toBeVerifiedRawResources.TryTake(out rawResource); }

        ~Memory()
        {
            _toBeExtractedHtmlDocuments?.Dispose();
            _toBeRenderedUris?.Dispose();
            _toBeVerifiedRawResources?.Dispose();
        }
    }
}