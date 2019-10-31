using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Core;
using Helix.Crawler.Abstractions;
using log4net;
using Newtonsoft.Json;

namespace Helix.Crawler
{
    public class CoordinatorBlock : TransformManyBlock<ProcessingResult, Resource>, ICoordinatorBlock, IDisposable
    {
        readonly HashSet<string> _alreadyProcessedUrls;
        readonly Configurations _configurations;
        readonly ILog _log;
        readonly object _memorizationLock;
        int _remainingWorkload;
        readonly IResourceEnricher _resourceEnricher;
        readonly StateMachine<WorkflowState, WorkflowCommand> _stateMachine;
        readonly IStatistics _statistics;

        public BufferBlock<Event> Events { get; }

        public override Task Completion => Task.WhenAll(base.Completion, Events.Completion);

        public int RemainingWorkload => _remainingWorkload;

        public CoordinatorBlock(Configurations configurations, IResourceEnricher resourceEnricher, IStatistics statistics, ILog log)
        {
            _log = log;
            _remainingWorkload = 0;
            _statistics = statistics;
            _memorizationLock = new object();
            _configurations = configurations;
            _resourceEnricher = resourceEnricher;
            _alreadyProcessedUrls = new HashSet<string>();

            _stateMachine = new StateMachine<WorkflowState, WorkflowCommand>(
                new Dictionary<Transition<WorkflowState, WorkflowCommand>, WorkflowState>
                {
                    { Transition(WorkflowState.WaitingForActivation, WorkflowCommand.Activate), WorkflowState.Activated },
                    { Transition(WorkflowState.Activated, WorkflowCommand.Deactivate), WorkflowState.WaitingForActivation }
                },
                WorkflowState.WaitingForActivation
            );

            Events = new BufferBlock<Event>(new DataflowBlockOptions { EnsureOrdered = true });
            base.Completion.ContinueWith(_ => Events.Complete());

            Transition<WorkflowState, WorkflowCommand> Transition(WorkflowState fromState, WorkflowCommand command)
            {
                return new Transition<WorkflowState, WorkflowCommand>(fromState, command);
            }
        }

        public void Dispose() { _stateMachine?.Dispose(); }

        public bool TryActivateWorkflow(string startUrl)
        {
            var activationSuccessful = false;
            var stateTransitionSucceeded = _stateMachine.TryTransitNext(WorkflowCommand.Activate, () =>
            {
                try
                {
                    Interlocked.Increment(ref _remainingWorkload);
                    activationSuccessful = this.Post(new SuccessfulProcessingResult
                    {
                        NewResources = new List<Resource>
                        {
                            _resourceEnricher.Enrich(new Resource
                            {
                                ParentUri = null,
                                IsInternal = true,
                                OriginalUrl = startUrl,
                                IsExtractedFromHtmlDocument = true
                            })
                        }
                    });

                    if (!activationSuccessful) throw new ArgumentException("Could not activate workflow using given start URL", startUrl);
                }
                catch (Exception exception)
                {
                    Interlocked.Decrement(ref _remainingWorkload);

                    _log.Error("One or more errors occurred when trying to activate workflow.", exception);
                    if (!_stateMachine.TryTransitNext(WorkflowCommand.Deactivate))
                        _log.StateTransitionFailureEvent(_stateMachine.CurrentState, WorkflowCommand.Deactivate);
                }
            });
            if (!stateTransitionSucceeded) _log.StateTransitionFailureEvent(_stateMachine.CurrentState, WorkflowCommand.Activate);

            return activationSuccessful;
        }

        protected override IEnumerable<Resource> Transform(ProcessingResult processingResult)
        {
            try
            {
                if (processingResult == null)
                    throw new ArgumentNullException(nameof(processingResult));

                var isNotStartResource = processingResult.ProcessedResource != null;
                if (isNotStartResource)
                {
                    CheckIfProcessedResourceWasRegistered();
                    UpdateStatistics();
                }

                var newlyDiscoveredResources = DiscoverNewResources().Where(r => r.IsInternal || _configurations.VerifyExternalUrls);
                Interlocked.Decrement(ref _remainingWorkload);

                if (isNotStartResource) SendOutResourceVerifiedEvent();
                if (_remainingWorkload != 0) return newlyDiscoveredResources;

                if (!Events.Post(new Event { EventType = EventType.NoMoreWorkToDo }))
                    _log.Error($"Failed to post data to buffer block named [{nameof(Events)}].");

                return new List<Resource>();

                #region Local Functions

                void CheckIfProcessedResourceWasRegistered()
                {
                    lock (_memorizationLock)
                    {
                        if (!_alreadyProcessedUrls.Contains(processingResult.ProcessedResource.GetAbsoluteUrl()))
                            throw new InvalidConstraintException($"Processed resource was not registered by {nameof(CoordinatorBlock)}.");
                    }
                }
                void UpdateStatistics()
                {
                    if (processingResult.ProcessedResource.StatusCode.IsWithinBrokenRange()) _statistics.IncrementBrokenUrlCount();
                    else _statistics.IncrementValidUrlCount();
                }
                void SendOutResourceVerifiedEvent()
                {
                    var processedResource = processingResult.ProcessedResource;
                    var resourceVerifiedEvent = new Event
                    {
                        EventType = EventType.ResourceVerified,
                        Message = $"{processedResource.StatusCode:D} - {processedResource.GetAbsoluteUrl()}"
                    };
                    if (!Events.Post(resourceVerifiedEvent) && !Events.Completion.IsCompleted)
                        _log.Error($"Failed to post data to buffer block named [{nameof(Events)}].");
                }
                IEnumerable<Resource> DiscoverNewResources()
                {
                    if (!(processingResult is SuccessfulProcessingResult successfulProcessingResult)) return new List<Resource>();

                    var newResources = new List<Resource>();
                    foreach (var newResource in successfulProcessingResult.NewResources)
                    {
                        lock (_memorizationLock)
                        {
                            if (_alreadyProcessedUrls.Contains(newResource.GetAbsoluteUrl())) continue;
                            _alreadyProcessedUrls.Add(newResource.GetAbsoluteUrl());
                        }
                        newResources.Add(newResource);
                        Interlocked.Increment(ref _remainingWorkload);
                    }
                    return newResources;
                }

                #endregion Local Functions
            }
            catch (Exception exception)
            {
                _log.Error($"One or more errors occurred while coordinating: {JsonConvert.SerializeObject(processingResult)}.", exception);
                return null;
            }
        }
    }
}