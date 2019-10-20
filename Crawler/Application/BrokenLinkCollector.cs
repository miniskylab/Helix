using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;
using log4net;

namespace Helix.Crawler
{
    public class BrokenLinkCollector : Application, IDisposable
    {
        IBrokenLinkCollectionWorkflow _brokenLinkCollectionWorkflow;
        readonly Task _brokenLinkCollectionWorkflowEventConsumingTask;
        readonly CancellationTokenSource _brokenLinkCollectionWorkflowEventConsumingTaskCts;
        readonly ILog _log;
        readonly StateMachine<CrawlerState, CrawlerCommand> _stateMachine;

        public CrawlerState CrawlerState => _stateMachine.CurrentState;

        public int RemainingWorkload => _brokenLinkCollectionWorkflow?.RemainingWorkload ?? 0;

        public static IStatistics Statistics => ServiceLocator.Get<IStatistics>();

        public event Action<Event> OnEventBroadcast;

        public BrokenLinkCollector()
        {
            _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            _stateMachine = new StateMachine<CrawlerState, CrawlerCommand>(PossibleTransitions(), CrawlerState.WaitingForInitialization);
            _brokenLinkCollectionWorkflowEventConsumingTaskCts = new CancellationTokenSource();

            var cancellationToken = _brokenLinkCollectionWorkflowEventConsumingTaskCts.Token;
            _brokenLinkCollectionWorkflowEventConsumingTask = new Task(() =>
            {
                var eventBroadcaster = ServiceLocator.Get<IEventBroadcaster>();
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var @event = _brokenLinkCollectionWorkflow.Events.Take(cancellationToken);
                        eventBroadcaster.Broadcast(@event);

                        if (@event.EventType == EventType.NoMoreWorkToDo)
                            Shutdown(CrawlerCommand.MarkAsRanToCompletion);
                    }
                }
                catch (Exception exception)
                {
                    if (exception.IsAcknowledgingOperationCancelledException(cancellationToken))
                        while (_brokenLinkCollectionWorkflow.Events.TryTake(out var @event))
                            eventBroadcaster.Broadcast(@event);
                    else
                        _log.Error("One or more errors occured while consuming event.", exception);
                }
            }, cancellationToken);

            Dictionary<Transition<CrawlerState, CrawlerCommand>, CrawlerState> PossibleTransitions()
            {
                return new Dictionary<Transition<CrawlerState, CrawlerCommand>, CrawlerState>
                {
                    { Transition(CrawlerState.WaitingForInitialization, CrawlerCommand.Stop), CrawlerState.Completed },
                    { Transition(CrawlerState.WaitingForInitialization, CrawlerCommand.Initialize), CrawlerState.WaitingToRun },
                    { Transition(CrawlerState.WaitingToRun, CrawlerCommand.Run), CrawlerState.Running },
                    { Transition(CrawlerState.WaitingToRun, CrawlerCommand.Abort), CrawlerState.WaitingForStop },
                    { Transition(CrawlerState.WaitingForStop, CrawlerCommand.Stop), CrawlerState.Completed },
                    { Transition(CrawlerState.Running, CrawlerCommand.Stop), CrawlerState.Completed },
                    { Transition(CrawlerState.Running, CrawlerCommand.Pause), CrawlerState.Paused },
                    { Transition(CrawlerState.Completed, CrawlerCommand.MarkAsRanToCompletion), CrawlerState.RanToCompletion },
                    { Transition(CrawlerState.Completed, CrawlerCommand.MarkAsCancelled), CrawlerState.Cancelled },
                    { Transition(CrawlerState.Completed, CrawlerCommand.MarkAsFaulted), CrawlerState.Faulted },
                    { Transition(CrawlerState.Paused, CrawlerCommand.Resume), CrawlerState.Running }
                };

                Transition<CrawlerState, CrawlerCommand> Transition(CrawlerState fromState, CrawlerCommand command)
                {
                    return new Transition<CrawlerState, CrawlerCommand>(fromState, command);
                }
            }
        }

        static BrokenLinkCollector()
        {
            // TODO: A workaround for .Net Core 2.x bug. Should be removed in the future.
            AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
        }

        public void Dispose()
        {
            _brokenLinkCollectionWorkflowEventConsumingTask?.Wait();
            _brokenLinkCollectionWorkflowEventConsumingTask?.Dispose();
            _brokenLinkCollectionWorkflowEventConsumingTaskCts?.Dispose();
            _stateMachine?.Dispose();
        }

        public void Stop() { Shutdown(CrawlerCommand.MarkAsCancelled); }

        public bool TryStart(Configurations configurations)
        {
            var tryStartResult = false;
            var stateTransitionSucceeded = _stateMachine.TryTransitNext(CrawlerCommand.Initialize, () =>
            {
                try
                {
                    _log.Info("Starting ...");
                    SetupAndConfigureServices();
                    RecreateDirectoryContainingScreenshotFiles();
                    StartHardwareMonitorService();

                    if (!TryActivateWorkflow())
                        throw new Exception("Failed to activate workflow.");

                    if (!_stateMachine.TryTransitNext(CrawlerCommand.Run))
                        _log.StateTransitionFailureEvent(_stateMachine.CurrentState, CrawlerCommand.Run);

                    tryStartResult = true;
                }
                catch (Exception exception)
                {
                    _log.Error($"One or more errors occurred in {nameof(TryStart)} method.", exception);

                    if (!_stateMachine.TryTransitNext(CrawlerCommand.Abort))
                        _log.StateTransitionFailureEvent(_stateMachine.CurrentState, CrawlerCommand.Abort);

                    ServiceLocator.Get<IBrokenLinkCollectionWorkflow>().SignalShutdown();
                    Shutdown(CrawlerCommand.MarkAsFaulted);
                }

                void SetupAndConfigureServices()
                {
                    _log.Info("Setting up and configuring services ...");
                    ServiceLocator.SetupAndConfigureServices(configurations);
                    ServiceLocator.Get<IEventBroadcaster>().OnEventBroadcast += @event =>
                    {
                        OnEventBroadcast?.Invoke(@event);
                        if (@event.EventType != EventType.ResourceVerified && !string.IsNullOrWhiteSpace(@event.Message))
                            _log.Info(@event.Message);
                    };

                    _brokenLinkCollectionWorkflow = ServiceLocator.Get<IBrokenLinkCollectionWorkflow>();
                }
                void RecreateDirectoryContainingScreenshotFiles()
                {
                    if (Directory.Exists(Configurations.PathToDirectoryContainsScreenshotFiles))
                        Directory.Delete(Configurations.PathToDirectoryContainsScreenshotFiles, true);
                    Directory.CreateDirectory(Configurations.PathToDirectoryContainsScreenshotFiles);
                }
                bool TryActivateWorkflow()
                {
                    var eventBroadcaster = ServiceLocator.Get<IEventBroadcaster>();
                    eventBroadcaster.Broadcast(StartProgressUpdatedEvent($"Activating {nameof(BrokenLinkCollectionWorkflow)} ..."));

                    _brokenLinkCollectionWorkflowEventConsumingTask.Start();
                    return ServiceLocator.Get<IBrokenLinkCollectionWorkflow>().TryActivate(configurations.StartUri.AbsoluteUri);
                }
                void StartHardwareMonitorService()
                {
                    ServiceLocator.Get<IEventBroadcaster>().Broadcast(StartProgressUpdatedEvent("Starting hardware monitor service ..."));
                    ServiceLocator.Get<IHardwareMonitor>().StartMonitoring();
                }
                Event StartProgressUpdatedEvent(string message)
                {
                    return new Event
                    {
                        EventType = EventType.StartProgressUpdated,
                        Message = message
                    };
                }
            });
            if (!stateTransitionSucceeded) _log.StateTransitionFailureEvent(_stateMachine.CurrentState, CrawlerCommand.Initialize);
            return tryStartResult;
        }

        void Shutdown(CrawlerCommand crawlerCommand)
        {
            var stateTransitionSucceeded = _stateMachine.TryTransitNext(CrawlerCommand.Stop, () =>
            {
                try
                {
                    StopHardwareMonitorService();
                    ShutdownWorkflow();
                    ReleaseResources();

                    if (!_stateMachine.TryTransitNext(crawlerCommand))
                        _log.StateTransitionFailureEvent(_stateMachine.CurrentState, crawlerCommand);

                    OnEventBroadcast?.Invoke(new Event
                    {
                        EventType = EventType.Completed,
                        Message = Enum.GetName(typeof(CrawlerState), CrawlerState)
                    });
                    _log.Info(Enum.GetName(typeof(CrawlerState), _stateMachine.CurrentState));
                }
                catch (Exception exception)
                {
                    _log.Error($"One or more errors occurred when stopping {nameof(BrokenLinkCollector)}.", exception);
                }

                #region Local Functions

                void StopHardwareMonitorService()
                {
                    try
                    {
                        var hardwareMonitor = ServiceLocator.Get<IHardwareMonitor>();
                        if (hardwareMonitor == null || !hardwareMonitor.IsRunning) return;

                        var eventBroadcaster = ServiceLocator.Get<IEventBroadcaster>();
                        eventBroadcaster.Broadcast(StopProgressUpdatedEvent("Stopping hardware monitor service ..."));

                        hardwareMonitor.StopMonitoring();
                    }
                    catch (Exception exception)
                    {
                        crawlerCommand = CrawlerCommand.MarkAsFaulted;
                        _log.Error("One or more errors occurred when stopping hardware monitor service.", exception);
                    }
                }
                void ShutdownWorkflow()
                {
                    try
                    {
                        var eventBroadcaster = ServiceLocator.Get<IEventBroadcaster>();
                        eventBroadcaster.Broadcast(StopProgressUpdatedEvent("Waiting for background tasks to complete ..."));

                        _brokenLinkCollectionWorkflow.SignalShutdown();
                        ServiceLocator.Get<CancellationTokenSource>().Cancel();

                        ServiceLocator.Get<IBrokenLinkCollectionWorkflow>().WaitForCompletion();
                        _brokenLinkCollectionWorkflowEventConsumingTaskCts.Cancel();
                    }
                    catch (Exception exception)
                    {
                        var workflowCancellationToken = ServiceLocator.Get<CancellationTokenSource>().Token;
                        if (exception.IsAcknowledgingOperationCancelledException(workflowCancellationToken)) return;

                        crawlerCommand = CrawlerCommand.MarkAsFaulted;
                        _log.Error("One or more errors occurred when shutting down workflow.", exception);
                    }
                }
                void ReleaseResources()
                {
                    try
                    {
                        ServiceLocator.Get<IEventBroadcaster>().Broadcast(StopProgressUpdatedEvent("Disposing services ..."));
                        ServiceLocator.DisposeServices();
                    }
                    catch (Exception exception)
                    {
                        crawlerCommand = CrawlerCommand.MarkAsFaulted;
                        _log.Error("One or more errors occurred when releasing resources.", exception);
                    }
                }
                Event StopProgressUpdatedEvent(string message)
                {
                    return new Event
                    {
                        EventType = EventType.StopProgressUpdated,
                        Message = message
                    };
                }

                #endregion Local Functions
            });
            if (!stateTransitionSucceeded) _log.StateTransitionFailureEvent(_stateMachine.CurrentState, CrawlerCommand.Stop);
        }
    }
}