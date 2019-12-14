using System;
using System.Collections.Generic;
using System.Linq;
using Helix.Bot.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Bot
{
    public class PostProcessorBlock : TransformBlock<RenderingResult, ProcessingResult>, IPostProcessorBlock
    {
        public PostProcessorBlock(IResourceExtractor resourceExtractor, IResourceScope resourceScope, ILog log)
        {
            _log = log;
            _resourceScope = resourceScope;
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
                    MillisecondsPageLoadTime = renderingResult.MillisecondsPageLoadTime,
                    ProcessedResource = renderingResult.RenderedResource,
                    NewResources = newResources.Select(newResource =>
                    {
                        if (newResource.StatusCode == default && IsOrphanedUri()) newResource.StatusCode = StatusCode.OrphanedUri;
                        if (newResource.Uri != null) newResource.IsInternal = _resourceScope.IsInternalResource(newResource);
                        return newResource;

                        #region Local Functions

                        bool IsOrphanedUri()
                        {
                            // TODO: Investigate where those orphaned Uri-s came from.
                            return newResource.ParentUri == null && !_resourceScope.IsStartUri(newResource.OriginalUri);
                        }

                        #endregion
                    }).ToList()
                };
            }
            catch (Exception exception)
            {
                _log.Error($"One or more errors occurred while processing: {JsonConvert.SerializeObject(renderingResult)}.", exception);
                return new FailedProcessingResult { ProcessedResource = renderingResult?.RenderedResource };
            }
        }

        #region Local Functions

        readonly ILog _log;
        readonly IResourceScope _resourceScope;
        readonly IResourceExtractor _resourceExtractor;

        #endregion
    }
}