using System;
using System.Collections.Generic;
using System.Threading;
using Helix.Crawler.Abstractions;
using log4net;

namespace Helix.Crawler
{
    public class ResourceExtractorBlock : TransformManyBlock<HtmlDocument, Resource>
    {
        readonly ILog _log;
        readonly IResourceExtractor _resourceExtractor;

        public ResourceExtractorBlock(CancellationToken cancellationToken, IResourceExtractor resourceExtractor, ILog log)
            : base(cancellationToken)
        {
            _log = log;
            _resourceExtractor = resourceExtractor;
        }

        protected override IEnumerable<Resource> Transform(HtmlDocument htmlDocument)
        {
            try { return _resourceExtractor.ExtractResourcesFrom(htmlDocument); }
            catch (Exception exception)
            {
                _log.Error($"One or more errors occurred while extracting resource from URL: {htmlDocument.Uri}.", exception);
                return null;
            }
        }
    }
}