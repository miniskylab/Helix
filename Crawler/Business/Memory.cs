using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class Memory : IMemory
    {
        readonly ConcurrentSet<string> _alreadyVerifiedUrls = new ConcurrentSet<string>();
        readonly BlockingCollection<HtmlDocument> _toBeExtractedHtmlDocuments = new BlockingCollection<HtmlDocument>(100000);
        readonly BlockingCollection<Uri> _toBeRenderedUris = new BlockingCollection<Uri>(100000);
        readonly BlockingCollection<RawResource> _toBeVerifiedRawResources = new BlockingCollection<RawResource>(100000);
        static readonly object SyncRoot = new object();

        public Configurations Configurations { get; }

        public string ErrorLogFilePath { get; }

        public int ToBeExtractedHtmlDocumentCount => _toBeExtractedHtmlDocuments.Count;

        public int ToBeRenderedUriCount => _toBeRenderedUris.Count;

        public int ToBeVerifiedRawResourceCount => _toBeVerifiedRawResources.Count;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Memory(Configurations configurations)
        {
            Configurations = configurations;
            ErrorLogFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "helix_errors.log");
            _alreadyVerifiedUrls.Clear();
            _alreadyVerifiedUrls.Add(Configurations.StartUri.AbsoluteUri);
            _toBeVerifiedRawResources.Add(
                new RawResource
                {
                    ParentUri = null,
                    Url = Configurations.StartUri.AbsoluteUri
                }
            );
        }

        public void Memorize(RawResource toBeVerifiedRawResource, CancellationToken cancellationToken)
        {
            lock (SyncRoot)
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

        public HtmlDocument TakeToBeExtractedHtmlDocument(CancellationToken cancellationToken)
        {
            return _toBeExtractedHtmlDocuments.Take(cancellationToken);
        }

        public Uri TakeToBeRenderedUri(CancellationToken cancellationToken) { return _toBeRenderedUris.Take(cancellationToken); }

        public RawResource TakeToBeVerifiedRawResource(CancellationToken cancellationToken)
        {
            return _toBeVerifiedRawResources.Take(cancellationToken);
        }

        ~Memory()
        {
            _toBeExtractedHtmlDocuments?.Dispose();
            _toBeRenderedUris?.Dispose();
            _toBeVerifiedRawResources?.Dispose();
        }
    }
}