using System;
using System.Collections.Generic;
using System.Threading;
using Helix.Crawler.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Crawler
{
    public class ProcessingResultGeneratorBlock : TransformBlock<RenderingResult, ProcessingResult>, IProcessingResultGeneratorBlock
    {
        readonly ILog _log;
        readonly IResourceExtractor _resourceExtractor;

        public ProcessingResultGeneratorBlock(CancellationToken cancellationToken, IResourceExtractor resourceExtractor, ILog log)
            : base(cancellationToken, maxDegreeOfParallelism: 300)
        {
            _log = log;
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
                    NewResources = newResources,
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