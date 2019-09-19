using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class CoordinatorBlock : TransformManyBlock<RenderingResult, Resource>
    {
        readonly HashSet<string> _alreadyProcessedUrls;
        readonly object _memorizationLock;
        int _remainingWorkload;

        public CoordinatorBlock(CancellationToken cancellationToken) : base(cancellationToken)
        {
            _remainingWorkload = 0;
            _memorizationLock = new object();
            _alreadyProcessedUrls = new HashSet<string>();
        }

        protected override IEnumerable<Resource> Transform(RenderingResult renderingResult)
        {
            var newResources = new List<Resource>();
            foreach (var newResource in renderingResult.NewResources)
            {
                lock (_memorizationLock)
                {
                    if (_alreadyProcessedUrls.Contains(newResource.GetAbsoluteUrl())) continue;
                    _alreadyProcessedUrls.Add(newResource.GetAbsoluteUrl());
                }
                newResources.Add(newResource);
                Interlocked.Increment(ref _remainingWorkload);
            }
            Interlocked.Decrement(ref _remainingWorkload);

            return new ReadOnlyCollection<Resource>(newResources);
        }
    }
}