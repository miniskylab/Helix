using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks.Dataflow;
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

        public void StopWorkflow()
        {
            // TODO: Add implementation
        }

        public bool TryActivateWorkflow(string startUrl)
        {
            // TODO: Move Crawler state machine here
            // TODO: Add a check so that, this method can only be called once

            var startResource = new Resource { ParentUri = null, OriginalUrl = startUrl };
            var startRenderingResult = new RenderingResult
            {
                HtmlDocument = null,
                NewResources = new List<Resource> { startResource }
            };
            return this.Post(startRenderingResult);
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