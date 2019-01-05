using System;
using System.Threading.Tasks;

namespace Helix.Crawler.Abstractions
{
    public delegate void AllAttemptsToCollectNewRawResourcesFailedEvent(IResource parentResource);
    public delegate void BrowserExceptionOccurredEvent(Exception webDriverException, IResource resourceThatTriggeredThisException);
    public delegate void IdleEvent();
    public delegate Task RawResourceCollectedEvent(IRawResource rawResource);
    public delegate void UrlCollectedEvent(string url);
}