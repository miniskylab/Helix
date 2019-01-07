using System;

namespace Helix.Crawler.Abstractions
{
    public interface IResourceCollector : IDisposable
    {
        event AllAttemptsToCollectNewRawResourcesFailedEvent OnAllAttemptsToCollectNewRawResourcesFailed;
        event BrowserExceptionOccurredEvent OnBrowserExceptionOccurred;
        event IdleEvent OnIdle;
        event RawResourceCollectedEvent OnRawResourceCollected;

        void CollectNewRawResourcesFrom(Resource parentResource);
    }
}