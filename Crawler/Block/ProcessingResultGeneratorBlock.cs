using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Helix.Crawler.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Crawler
{
    public class ProcessingResultGeneratorBlock : TransformBlock<RenderingResult, ProcessingResult>, IProcessingResultGeneratorBlock
    {
        readonly ILog _log;
        readonly IResourceEnricher _resourceEnricher;
        readonly IResourceExtractor _resourceExtractor;

        public ProcessingResultGeneratorBlock(CancellationToken cancellationToken, IResourceExtractor resourceExtractor, ILog log,
            IResourceEnricher resourceEnricher) : base(cancellationToken)
        {
            _log = log;
            _resourceEnricher = resourceEnricher;
            _resourceExtractor = resourceExtractor;
        }

        protected override ProcessingResult Transform(RenderingResult renderingResult)
        {
            try
            {
                if (renderingResult == null)
                    throw new ArgumentNullException(nameof(renderingResult));

                var newResources = new List<Resource>();
                newResources.AddRange(renderingResult.CapturedResources);
                newResources.AddRange(_resourceExtractor.ExtractResourcesFrom(renderingResult.HtmlDocument));

                return new SuccessfulProcessingResult
                {
                    NewResources = newResources.Select(_resourceEnricher.Enrich).ToList(),
                    ProcessedResource = renderingResult.RenderedResource
                };
            }
            catch (Exception exception)
            {
                _log.Error($"One or more errors occurred while processing: {JsonConvert.SerializeObject(renderingResult)}.", exception);
                return new FailedProcessingResult { ProcessedResource = renderingResult?.RenderedResource };
            }
        }
    }
}