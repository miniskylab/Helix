using System;
using System.Threading.Tasks.Dataflow;
using Helix.Crawler.Abstractions;
using log4net;

namespace Helix.Crawler
{
    public class CrawlerBotV2
    {
        readonly ILog _log;
        readonly IResourceEnricher _resourceEnricher;
        readonly IResourceVerifier _resourceVerifier;

        TransformBlock<Resource, Resource> ResourceEnricher => new TransformBlock<Resource, Resource>(
            resource =>
            {
                try { return _resourceEnricher.Enrich(resource); }
                catch (Exception exception)
                {
                    _log.Error($"One or more errors occurred in {nameof(ResourceEnricher)} block.", exception);
                    return null;
                }
            },
            new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }
        );
    }
}