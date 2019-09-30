using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Helix.Core;
using Helix.Crawler.Abstractions;
using log4net;

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
                    { Transition(WorkflowState.WaitingForInitialization, WorkflowCommand.Initialize), WorkflowState.WaitingToRun },
                    { Transition(WorkflowState.WaitingToRun, WorkflowCommand.Abort), WorkflowState.WaitingForInitialization },
                    { Transition(WorkflowState.WaitingToRun, WorkflowCommand.Run), WorkflowState.Running },
                    { Transition(WorkflowState.Running, WorkflowCommand.Stop), WorkflowState.Completed },
                    { Transition(WorkflowState.Completed, WorkflowCommand.MarkAsRanToCompletion), WorkflowState.RanToCompletion },
                    { Transition(WorkflowState.Completed, WorkflowCommand.MarkAsCancelled), WorkflowState.Cancelled },
                    { Transition(WorkflowState.Completed, WorkflowCommand.MarkAsFaulted), WorkflowState.Faulted }
                },
                WorkflowState.WaitingToRun
            );

            Transition<WorkflowState, WorkflowCommand> Transition(WorkflowState fromState, WorkflowCommand command)
            {
                return new Transition<WorkflowState, WorkflowCommand>(fromState, command);
            }
        }

        public void StopWorkflow()
        {
            // TODO: Add implementation
        }

        public bool TryActivateWorkflow(string startUrl)
        {
            var activationSuccessful = false;
            var stateTransitionSucceeded = _stateMachine.TryTransitNext(WorkflowCommand.Initialize, () =>
            {
                try
                {
                    activationSuccessful = this.Post(
                        new RenderingResult
                        {
                            HtmlDocument = null,
                            NewResources = new List<Resource>
                            {
                                new Resource
                                {
                                    ParentUri = null,
                                    OriginalUrl = startUrl
                                }
                            }
                        }
                    );

                    if (!activationSuccessful) throw new ArgumentException("Could not activate workflow using given start URL", startUrl);

                    if (!_stateMachine.TryTransitNext(WorkflowCommand.Run))
                        _log.StateTransitionFailureEvent(_stateMachine.CurrentState, WorkflowCommand.Run);
                }
                catch (Exception exception)
                {
                    _log.Error("One or more errors occurred when trying to activate workflow.", exception);

                    if (!_stateMachine.TryTransitNext(WorkflowCommand.Abort))
                        _log.StateTransitionFailureEvent(_stateMachine.CurrentState, WorkflowCommand.Abort);
                }
            });
            if (!stateTransitionSucceeded) _log.StateTransitionFailureEvent(_stateMachine.CurrentState, WorkflowCommand.Initialize);

            return activationSuccessful;
        }

        protected override IEnumerable<Resource> Transform(RenderingResult renderingResult)
        {
            var newResources = new List<Resource>();
            foreach (var newResource in renderingResult.NewResources)
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

            return new ReadOnlyCollection<Resource>(newResources);
        }
    }
}