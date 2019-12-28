using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Helix.Bot.Abstractions;
using Helix.Core;
using log4net;

namespace Helix.Bot
{
    public class BrokenLinkCollector : Bot, IDisposable
    {
        readonly StateMachine<BotState, BotCommand> _stateMachine;

        public BotState BotState => _stateMachine.CurrentState;

        public event Action<Event> OnEventBroadcast;

        public BrokenLinkCollector()
        {
            _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            _stateMachine = new StateMachine<BotState, BotCommand>(PossibleTransitions(), BotState.WaitingForInitialization);

            #region Local Functions

            static Dictionary<Transition<BotState, BotCommand>, BotState> PossibleTransitions()
            {
                return new Dictionary<Transition<BotState, BotCommand>, BotState>
                {
                    { Transition(BotState.WaitingForInitialization, BotCommand.Stop), BotState.Completed },
                    { Transition(BotState.WaitingForInitialization, BotCommand.Initialize), BotState.WaitingToRun },
                    { Transition(BotState.WaitingToRun, BotCommand.Run), BotState.Running },
                    { Transition(BotState.WaitingToRun, BotCommand.Abort), BotState.WaitingForStop },
                    { Transition(BotState.WaitingForStop, BotCommand.Stop), BotState.Completed },
                    { Transition(BotState.Running, BotCommand.Stop), BotState.Completed },
                    { Transition(BotState.Completed, BotCommand.MarkAsRanToCompletion), BotState.RanToCompletion },
                    { Transition(BotState.Completed, BotCommand.MarkAsCancelled), BotState.Cancelled },
                    { Transition(BotState.Completed, BotCommand.MarkAsFaulted), BotState.Faulted }
                };

                static Transition<BotState, BotCommand> Transition(BotState fromState, BotCommand command)
                {
                    return new Transition<BotState, BotCommand>(fromState, command);
                }
            }

            #endregion
        }

        public void Dispose() { _stateMachine?.Dispose(); }

        public void Stop() { Shutdown(BotCommand.MarkAsCancelled); }

        public bool TryStart(Configurations configurations)
        {
            var tryStartResult = false;
            var stateTransitionSucceeded = _stateMachine.TryTransitNext(BotCommand.Initialize, () =>
            {
                try
                {
                    Initialize();
                    RecreateDirectoryContainingScreenshotFiles();
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

                    Shutdown(BotCommand.MarkAsFaulted);
                }

                #region Local Functions

                void Initialize()
                {
                    Broadcast(new StartProgressReportEvent { Message = $"Initializing {nameof(BrokenLinkCollectionWorkflow)} ..." });
                    SetupServiceContainer(configurations);

                    _brokenLinkCollectionWorkflow = GetWorkflow<IBrokenLinkCollectionWorkflow>();
                    _brokenLinkCollectionWorkflow.OnEventBroadcast += @event =>
                    {
                        Broadcast(@event);
                        switch (@event)
                        {
                            case NoMoreWorkToDoEvent _:
                                Task.Run(() => Shutdown(BotCommand.MarkAsRanToCompletion));
                                break;
                            case RedirectHappenedAtStartUrlEvent _:
                                Task.Run(() => Shutdown(BotCommand.MarkAsFaulted));
                                break;
                        }
                    };
                }
                void RecreateDirectoryContainingScreenshotFiles()
                {
                    if (Directory.Exists(configurations.PathToDirectoryContainsScreenshotFiles))
                        Directory.Delete(configurations.PathToDirectoryContainsScreenshotFiles, true);
                    Directory.CreateDirectory(configurations.PathToDirectoryContainsScreenshotFiles);
                }
                void ActivateWorkflow()
                {
                    Broadcast(new StartProgressReportEvent { Message = $"Activating {nameof(BrokenLinkCollectionWorkflow)} ..." });
                    if (!_brokenLinkCollectionWorkflow.TryActivate(configurations.StartUri.AbsoluteUri))
                        throw new Exception("Failed to activate workflow.");

                    Broadcast(new WorkflowActivatedEvent());
                }

                #endregion
            });
            if (!stateTransitionSucceeded) _log.StateTransitionFailureEvent(_stateMachine.CurrentState, BotCommand.Initialize);
            return tryStartResult;
        }

        void Broadcast(Event @event)
        {
            OnEventBroadcast?.Invoke(@event);
            if (!(@event is ResourceProcessedEvent) && !string.IsNullOrWhiteSpace(@event.Message))
                _log.Info(@event.Message);
        }

        void Shutdown(BotCommand botCommand)
        {
            var stateTransitionSucceeded = _stateMachine.TryTransitNext(BotCommand.Stop, () =>
            {
                try
                {
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

                void ShutdownWorkflow()
                {
                    try
                    {
                        Broadcast(StopProgressReportEvent("Waiting for background tasks to complete ..."));
                        _brokenLinkCollectionWorkflow?.Shutdown();
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
                        Broadcast(StopProgressReportEvent("Disposing services ..."));
                        DisposeServiceContainer();
                    }
                    catch (Exception exception)
                    {
                        botCommand = BotCommand.MarkAsFaulted;
                        _log.Error("One or more errors occurred when releasing resources.", exception);
                    }
                }
                static Event StopProgressReportEvent(string message) { return new StopProgressReportEvent { Message = message }; }

                #endregion
            });
            if (!stateTransitionSucceeded) _log.StateTransitionFailureEvent(_stateMachine.CurrentState, BotCommand.Stop);
        }

        #region Injected Services

        readonly ILog _log;
        IBrokenLinkCollectionWorkflow _brokenLinkCollectionWorkflow;

        #endregion
    }
}