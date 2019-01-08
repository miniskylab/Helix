using System;
using System.Threading.Tasks;

namespace Helix.Crawler.Abstractions
{
    // TODO: Replace with System.Action or System.Func delegates
    public delegate void AllAttemptsToCollectNewRawResourcesFailedEvent(Resource parentResource);
    public delegate void BrowserExceptionOccurredEvent(Exception webDriverException, Resource resourceThatTriggeredThisException);
    public delegate void IdleEvent();
    public delegate Task RawResourceCollectedEvent(RawResource rawResource);
}