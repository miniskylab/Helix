using System;

namespace Helix.Abstractions
{
    public interface IResourceCollector : IDisposable
    {
        event AllAttemptsToCollectNewRawResourcesFailedEvent OnAllAttemptsToCollectNewRawResourcesFailed;
        event BrowserExceptionOccurredEvent OnBrowserExceptionOccurred;
        event IdleEvent OnIdle;
        event RawResourceCollectedEvent OnRawResourceCollected;

        void CollectNewRawResourcesFrom(IResource parentResource);
    }
}