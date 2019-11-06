using System;
using System.Collections.Generic;
using System.Linq;
using Helix.Bot.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Bot
{
    public class ProcessingResultGeneratorBlock : TransformBlock<RenderingResult, ProcessingResult>, IProcessingResultGeneratorBlock
    {
        readonly ILog _log;
        readonly IResourceEnricher _resourceEnricher;
        readonly IResourceExtractor _resourceExtractor;

        public ProcessingResultGeneratorBlock(IResourceExtractor resourceExtractor, IResourceEnricher resourceEnricher, ILog log)
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
                    ProcessedResource = renderingResult.RenderedResource,
                    NewResources = newResources.Select(_resourceEnricher.Enrich).ToList()
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