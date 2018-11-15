using System;

namespace Helix.Abstractions
{
    public interface IResourceCollector : IDisposable
    {
        event AllAttemptsToCollectNewRawResourcesFailedEvent OnAllAttemptsToCollectNewRawResourcesFailed;
        event ExceptionOccurredEvent OnExceptionOccurred;
        event IdleEvent OnIdle;
        event NetworkTrafficCapturedEvent OnNetworkTrafficCaptured;
        event RawResourceCollectedEvent OnRawResourceCollected;

        void CollectNewRawResourcesFrom(IResource parentResource);
    }
}