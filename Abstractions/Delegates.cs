using OpenQA.Selenium;
using Titanium.Web.Proxy.EventArguments;

namespace Helix.Abstractions
{
    public delegate void AllAttemptsToCollectNewRawResourcesFailedEvent(IResource parentResource);
    public delegate void ExceptionOccurredEvent(WebDriverException webDriverException, IResource resourceThatTriggeredThisException);
    public delegate void IdleEvent();
    public delegate void NetworkTrafficCapturedEvent(SessionEventArgsBase networkTraffic);
    public delegate void RawResourceCollectedEvent(IRawResource rawResource);
}