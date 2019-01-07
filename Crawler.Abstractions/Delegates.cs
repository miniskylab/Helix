using System;
using System.Threading.Tasks;

namespace Helix.Crawler.Abstractions
{
    public delegate void AllAttemptsToCollectNewRawResourcesFailedEvent(Resource parentResource);
    public delegate void BrowserExceptionOccurredEvent(Exception webDriverException, Resource resourceThatTriggeredThisException);
    public delegate void IdleEvent();
    public delegate Task RawResourceCollectedEvent(RawResource rawResource);
    public delegate void UrlCollectedEvent(string url);
}