using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Crawler.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Crawler
{
    internal class ResourceEnricherBlock : TransformBlock<Resource, Resource>
    {
        readonly ILog _log;
        readonly IResourceEnricher _resourceEnricher;

        public BufferBlock<FailedProcessingResult> FailedProcessingResults { get; }

        public override Task Completion => Task.WhenAll(base.Completion, FailedProcessingResults.Completion);

        public ResourceEnricherBlock(CancellationToken cancellationToken, IResourceEnricher resourceEnricher, ILog log)
            : base(cancellationToken, maxDegreeOfParallelism: 300)
        {
            _log = log;
            _resourceEnricher = resourceEnricher;

            var generalDataflowBlockOptions = new DataflowBlockOptions { CancellationToken = cancellationToken };
            FailedProcessingResults = new BufferBlock<FailedProcessingResult>(generalDataflowBlockOptions);

            base.Completion.ContinueWith(_ => { FailedProcessingResults.Complete(); });
        }

        protected override Resource Transform(Resource resource)
        {
            try
            {
                if (resource == null) throw new ArgumentNullException(nameof(resource));
                return _resourceEnricher.Enrich(resource);
            }
            catch (Exception exception)
            {
                if (!FailedProcessingResults.Post(new FailedProcessingResult { ProcessedResource = resource }))
                    _log.Error($"Failed to post data to buffer block named [{nameof(FailedProcessingResults)}].");

                _log.Error($"One or more errors occurred while enriching: {JsonConvert.SerializeObject(resource)}.", exception);
                return null;
            }
        }
    }
}