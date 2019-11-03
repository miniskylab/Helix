using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Helix.Bot.Abstractions;
using Helix.Core;
using log4net;

namespace Helix.Bot
{
    public class BrokenLinkCollector : Application, IDisposable
    {
        IBrokenLinkCollectionWorkflow _brokenLinkCollectionWorkflow;
        readonly Task _brokenLinkCollectionWorkflowEventConsumingTask;
        readonly CancellationTokenSource _brokenLinkCollectionWorkflowEventConsumingTaskCts;
        readonly ILog _log;
        readonly StateMachine<BotState, BotCommand> _stateMachine;

        public BotState BotState => _stateMachine.CurrentState;

        public event Action<Event> OnEventBroadcast;

        public BrokenLinkCollector()
        {
            _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            _stateMachine = new StateMachine<BotState, BotCommand>(PossibleTransitions(), BotState.WaitingForInitialization);
            _brokenLinkCollectionWorkflowEventConsumingTaskCts = new CancellationTokenSource();

            var cancellationToken = _brokenLinkCollectionWorkflowEventConsumingTaskCts.Token;
            _brokenLinkCollectionWorkflowEventConsumingTask = new Task(ConsumeBrokenLinkCollectionWorkflowEvents, cancellationToken);

            #region Local Functions

            Dictionary<Transition<BotState, BotCommand>, BotState> PossibleTransitions()
            {
                return new Dictionary<Transition<BotState, BotCommand>, BotState>
                {
                    { Transition(BotState.WaitingForInitialization, BotCommand.Stop), BotState.Completed },
                    { Transition(BotState.WaitingForInitialization, BotCommand.Initialize), BotState.WaitingToRun },
                    { Transition(BotState.WaitingToRun, BotCommand.Run), BotState.Running },
                    { Transition(BotState.WaitingToRun, BotCommand.Abort), BotState.WaitingForStop },
                    { Transition(BotState.WaitingForStop, BotCommand.Stop), BotState.Completed },
                    { Transition(BotState.Running, BotCommand.Stop), BotState.Completed },
                    { Transition(BotState.Running, BotCommand.Pause), BotState.Paused },
                    { Transition(BotState.Completed, BotCommand.MarkAsRanToCompletion), BotState.RanToCompletion },
                    { Transition(BotState.Completed, BotCommand.MarkAsCancelled), BotState.Cancelled },
                    { Transition(BotState.Completed, BotCommand.MarkAsFaulted), BotState.Faulted },
                    { Transition(BotState.Paused, BotCommand.Resume), BotState.Running }
                };

                Transition<BotState, BotCommand> Transition(BotState fromState, BotCommand command)
                {
                    return new Transition<BotState, BotCommand>(fromState, command);
                }
            }
            void ConsumeBrokenLinkCollectionWorkflowEvents()
            {
                var eventBroadcaster = ServiceLocator.Get<IEventBroadcaster>();
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var @event = _brokenLinkCollectionWorkflow.Events.Take(cancellationToken);
                        eventBroadcaster.Broadcast(@event);

                        if (@event is NoMoreWorkToDoEvent)
                            Shutdown(BotCommand.MarkAsRanToCompletion);
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
            }

            #endregion
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

        public void Stop() { Shutdown(BotCommand.MarkAsCancelled); }

        public bool TryStart(Configurations configurations)
        {
            var tryStartResult = false;
            var stateTransitionSucceeded = _stateMachine.TryTransitNext(BotCommand.Initialize, () =>
            {
                try
                {
                    _log.Info("Starting ...");
                    SetupAndConfigureServices();
                    RecreateDirectoryContainingScreenshotFiles();
                    StartHardwareMonitorService();
                    ActivateWorkflow();

                    if (!_stateMachine.TryTransitNext(BotCommand.Run))
                        _log.StateTransitionFailureEvent(_stateMachine.CurrentState, BotCommand.Run);

                    tryStartResult = true;
                }
                catch (Exception exception)
                {
                    _log.Error($"One or more errors occurred in {nameof(TryStart)} method.", exception);

                    if (!_stateMachine.TryTransitNext(BotCommand.Abort))
                        _log.StateTransitionFailureEvent(_stateMachine.CurrentState, BotCommand.Abort);

                    _brokenLinkCollectionWorkflow.Shutdown();
                    Shutdown(BotCommand.MarkAsFaulted);
                }

                #region Local Functions

                void SetupAndConfigureServices()
                {
                    _log.Info("Setting up and configuring services ...");
                    ServiceLocator.SetupAndConfigureServices(configurations);
                    ServiceLocator.Get<IEventBroadcaster>().OnEventBroadcast += @event =>
                    {
                        OnEventBroadcast?.Invoke(@event);
                        if (!(@event is WorkingProgressReportEvent) && !string.IsNullOrWhiteSpace(@event.Message))
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
                void ActivateWorkflow()
                {
                    var eventBroadcaster = ServiceLocator.Get<IEventBroadcaster>();
                    eventBroadcaster.Broadcast(StartProgressReportEvent($"Activating {nameof(BrokenLinkCollectionWorkflow)} ..."));

                    _brokenLinkCollectionWorkflowEventConsumingTask.Start();

                    if (!_brokenLinkCollectionWorkflow.TryActivate(configurations.StartUri.AbsoluteUri))
                        throw new Exception("Failed to activate workflow.");

                    eventBroadcaster.Broadcast(new WorkflowActivatedEvent());
                }
                void StartHardwareMonitorService()
                {
                    ServiceLocator.Get<IEventBroadcaster>().Broadcast(StartProgressReportEvent("Starting hardware monitor service ..."));
                    ServiceLocator.Get<IHardwareMonitor>().StartMonitoring();
                }
                Event StartProgressReportEvent(string message) { return new StartProgressReportEvent { Message = message }; }

                #endregion
            });
            if (!stateTransitionSucceeded) _log.StateTransitionFailureEvent(_stateMachine.CurrentState, BotCommand.Initialize);
            return tryStartResult;
        }

        void Shutdown(BotCommand botCommand)
        {
            var stateTransitionSucceeded = _stateMachine.TryTransitNext(BotCommand.Stop, () =>
            {
                try
                {
                    StopHardwareMonitorService();
                    ShutdownWorkflow();
                    ReleaseResources();

                    if (!_stateMachine.TryTransitNext(botCommand))
                        _log.StateTransitionFailureEvent(_stateMachine.CurrentState, botCommand);

                    OnEventBroadcast?.Invoke(new WorkflowCompletedEvent { Message = Enum.GetName(typeof(BotState), BotState) });
                    _log.Info(Enum.GetName(typeof(BotState), _stateMachine.CurrentState));
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
                        eventBroadcaster.Broadcast(StopProgressReportEvent("Stopping hardware monitor service ..."));

                        hardwareMonitor.StopMonitoring();
                    }
                    catch (Exception exception)
                    {
                        botCommand = BotCommand.MarkAsFaulted;
                        _log.Error("One or more errors occurred when stopping hardware monitor service.", exception);
                    }
                }
                void ShutdownWorkflow()
                {
                    try
                    {
                        var eventBroadcaster = ServiceLocator.Get<IEventBroadcaster>();
                        eventBroadcaster.Broadcast(StopProgressReportEvent("Waiting for background tasks to complete ..."));

                        _brokenLinkCollectionWorkflow.Shutdown();
                        _brokenLinkCollectionWorkflowEventConsumingTaskCts.Cancel();
                    }
                    catch (Exception exception)
                    {
                        botCommand = BotCommand.MarkAsFaulted;
                        _log.Error("One or more errors occurred when shutting down workflow.", exception);
                    }
                }
                void ReleaseResources()
                {
                    try
                    {
                        ServiceLocator.Get<IEventBroadcaster>().Broadcast(StopProgressReportEvent("Disposing services ..."));
                        ServiceLocator.DisposeServices();
                    }
                    catch (Exception exception)
                    {
                        botCommand = BotCommand.MarkAsFaulted;
                        _log.Error("One or more errors occurred when releasing resources.", exception);
                    }
                }
                Event StopProgressReportEvent(string message) { return new StopProgressReportEvent { Message = message }; }

                #endregion Local Functions
            });
            if (!stateTransitionSucceeded) _log.StateTransitionFailureEvent(_stateMachine.CurrentState, BotCommand.Stop);
        }
    }
}