using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Helix.Core;
using Helix.Crawler.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Crawler
{
    public class CoordinatorBlock : TransformManyBlock<RenderingResult, Resource>
    {
        readonly HashSet<string> _alreadyProcessedUrls;
        readonly ILog _log;
        readonly object _memorizationLock;
        int _remainingWorkload;
        readonly StateMachine<WorkflowState, WorkflowCommand> _stateMachine;

        public CoordinatorBlock(CancellationToken cancellationToken, ILog log) : base(cancellationToken)
        {
            _log = log;
            _remainingWorkload = 0;
            _memorizationLock = new object();
            _alreadyProcessedUrls = new HashSet<string>();

            _stateMachine = new StateMachine<WorkflowState, WorkflowCommand>(
                new Dictionary<Transition<WorkflowState, WorkflowCommand>, WorkflowState>
                {
                    { Transition(WorkflowState.WaitingForActivation, WorkflowCommand.Activate), WorkflowState.Activated },
                    { Transition(WorkflowState.Activated, WorkflowCommand.Deactivate), WorkflowState.WaitingForActivation },
                    { Transition(WorkflowState.Activated, WorkflowCommand.SignalShutdown), WorkflowState.SignaledForShutdown }
                },
                WorkflowState.WaitingForActivation
            );

            Transition<WorkflowState, WorkflowCommand> Transition(WorkflowState fromState, WorkflowCommand command)
            {
                return new Transition<WorkflowState, WorkflowCommand>(fromState, command);
            }
        }

        public void SignalShutdown()
        {
            var stateTransitionSucceeded = _stateMachine.TryTransitNext(WorkflowCommand.SignalShutdown, () =>
            {
                try { Complete(); }
                catch (Exception exception)
                {
                    _log.Error("One or more errors occurred while signaling shutdown for workflow.", exception);
                }
            });
            if (!stateTransitionSucceeded) _log.StateTransitionFailureEvent(_stateMachine.CurrentState, WorkflowCommand.SignalShutdown);
        }

        public bool TryActivateWorkflow(string startUrl)
        {
            var activationSuccessful = false;
            var stateTransitionSucceeded = _stateMachine.TryTransitNext(WorkflowCommand.Activate, () =>
            {
                try
                {
                    activationSuccessful = this.Post(new RenderingResult
                    {
                        HtmlDocument = null,
                        CapturedResources = new List<Resource> { new Resource { ParentUri = null, OriginalUrl = startUrl } }
                    });

                    if (!activationSuccessful) throw new ArgumentException("Could not activate workflow using given start URL", startUrl);
                }
                catch (Exception exception)
                {
                    _log.Error("One or more errors occurred when trying to activate workflow.", exception);
                    if (!_stateMachine.TryTransitNext(WorkflowCommand.Deactivate))
                        _log.StateTransitionFailureEvent(_stateMachine.CurrentState, WorkflowCommand.Deactivate);
                }
            });
            if (!stateTransitionSucceeded) _log.StateTransitionFailureEvent(_stateMachine.CurrentState, WorkflowCommand.Activate);

            return activationSuccessful;
        }

        protected override IEnumerable<Resource> Transform(RenderingResult renderingResult)
        {
            try
            {
                var newResources = new List<Resource>();
                foreach (var newResource in renderingResult.CapturedResources)
                {
                    lock (_memorizationLock)
                    {
                        if (_alreadyProcessedUrls.Contains(newResource.GetAbsoluteUrl())) continue;
                        _alreadyProcessedUrls.Add(newResource.GetAbsoluteUrl());
                    }
                    newResources.Add(newResource);
                    Interlocked.Increment(ref _remainingWorkload);
                }

                Interlocked.Decrement(ref _remainingWorkload);
                if (_remainingWorkload == 0) SignalShutdown();

                return new ReadOnlyCollection<Resource>(newResources);
            }
            catch (Exception exception)
            {
                _log.Error($"One or more errors occurred while coordinating: {JsonConvert.SerializeObject(renderingResult)}.", exception);
                return null;
            }
        }
    }
}