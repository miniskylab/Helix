using System;
using System.Threading;
using Helix.Crawler.Abstractions;
using log4net;

namespace Helix.Crawler
{
    internal class ResourceEnricherBlock : TransformBlock<Resource, Resource>
    {
        readonly ILog _log;
        readonly IResourceEnricher _resourceEnricher;

        public ResourceEnricherBlock(CancellationToken cancellationToken, IResourceEnricher resourceEnricher, ILog log)
            : base(cancellationToken)
        {
            _log = log;
            _resourceEnricher = resourceEnricher;
        }

        protected override Resource Transform(Resource resource)
        {
            try { return _resourceEnricher.Enrich(resource); }
            catch (Exception exception)
            {
                _log.Error($"One or more errors occurred while enriching URL: {resource.Uri}.", exception);
                return null;
            }
        }
    }
}