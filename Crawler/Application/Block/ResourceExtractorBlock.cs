using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Crawler.Abstractions;
using log4net;

namespace Helix.Crawler
{
    public class ResourceExtractorBlock : ActionBlock<HtmlDocument>
    {
        readonly ILog _log;
        readonly IResourceExtractor _resourceExtractor;

        public BufferBlock<Resource> ExtractedResources { get; }

        public override Task Completion => Task.WhenAll(base.Completion, ExtractedResources.Completion);

        public ResourceExtractorBlock(CancellationToken cancellationToken, IResourceExtractor resourceExtractor, ILog log)
            : base(cancellationToken)
        {
            _log = log;
            _resourceExtractor = resourceExtractor;

            ExtractedResources = new BufferBlock<Resource>(new DataflowBlockOptions { CancellationToken = cancellationToken });
            base.Completion.ContinueWith(_ => { ExtractedResources.Complete(); });
        }

        protected override void Act(HtmlDocument htmlDocument)
        {
            try
            {
                _resourceExtractor.ExtractResourcesFrom(htmlDocument, resource =>
                {
                    if (!ExtractedResources.Post(resource))
                        _log.Error($"Failed to post data to buffer block named [{nameof(ExtractedResources)}].");
                });
            }
            catch (Exception exception)
            {
                _log.Error($"One or more errors occurred while extracting resource from URL: {htmlDocument.Uri}.", exception);
            }
        }
    }
}