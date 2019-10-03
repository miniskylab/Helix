using System;
using System.Threading;
using Helix.Crawler.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Crawler
{
    public class RenderingResultEnricherBlock : TransformBlock<RenderingResult, RenderingResult>
    {
        readonly ILog _log;
        readonly IResourceExtractor _resourceExtractor;

        public RenderingResultEnricherBlock(CancellationToken cancellationToken, IResourceExtractor resourceExtractor, ILog log)
            : base(cancellationToken, maxDegreeOfParallelism: 300)
        {
            _log = log;
            _resourceExtractor = resourceExtractor;
        }

        protected override RenderingResult Transform(RenderingResult renderingResult)
        {
            try
            {
                var extractedResources = _resourceExtractor.ExtractResourcesFrom(renderingResult.HtmlDocument);
                renderingResult.CapturedResources.AddRange(extractedResources);
                return renderingResult;
            }
            catch (Exception exception)
            {
                _log.Error($"One or more errors occurred while enriching: {JsonConvert.SerializeObject(renderingResult)}.", exception);
                return null;
            }
        }
    }
}