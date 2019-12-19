using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Bot.Abstractions;
using Helix.Core;
using log4net;
using Newtonsoft.Json;

namespace Helix.Bot
{
    public class CoordinatorBlock : TransformManyBlock<ProcessingResult, Resource>, ICoordinatorBlock, IDisposable
    {
        readonly ConcurrentDictionary<string, bool> _processedUrls;
        readonly StateMachine<WorkflowState, WorkflowCommand> _stateMachine;

        public BufferBlock<Event> Events { get; }

        public BufferBlock<(ReportWritingAction, VerificationResult)> ReportWritingMessages { get; }

        public override Task Completion => Task.WhenAll(base.Completion, ReportWritingMessages.Completion, Events.Completion);

        public CoordinatorBlock(IStatistics statistics, IResourceScope resourceScope, IIncrementalIdGenerator incrementalIdGenerator,
            ILog log)
        {
            _log = log;
            _statistics = statistics;
            _resourceScope = resourceScope;
            _incrementalIdGenerator = incrementalIdGenerator;
            _processedUrls = new ConcurrentDictionary<string, bool>();

            _stateMachine = NewStateMachine();
            Events = new BufferBlock<Event>(new DataflowBlockOptions { EnsureOrdered = true });
            ReportWritingMessages = new BufferBlock<(ReportWritingAction, VerificationResult)>();

            #region Local Functions

            StateMachine<WorkflowState, WorkflowCommand> NewStateMachine()
            {
                return new StateMachine<WorkflowState, WorkflowCommand>(
                    new Dictionary<Transition<WorkflowState, WorkflowCommand>, WorkflowState>
                    {
                        { Transition(WorkflowState.WaitingForActivation, WorkflowCommand.Activate), WorkflowState.Activated },
                        { Transition(WorkflowState.Activated, WorkflowCommand.Deactivate), WorkflowState.WaitingForActivation }
                    },
                    WorkflowState.WaitingForActivation
                );

                #region Local Functions

                Transition<WorkflowState, WorkflowCommand> Transition(WorkflowState fromState, WorkflowCommand command)
                {
                    return new Transition<WorkflowState, WorkflowCommand>(fromState, command);
                }

                #endregion
            }

            #endregion
        }

        public override void Complete()
        {
            base.Complete();
            TryReceiveAll(out _);

            base.Completion.Wait();
            ReportWritingMessages.Complete();
            Events.Complete();
        }

        public void Dispose() { _stateMachine?.Dispose(); }

        public bool TryActivateWorkflow(string startUrl)
        {
            var activationSuccessful = false;
            var stateTransitionSucceeded = _stateMachine.TryTransitNext(WorkflowCommand.Activate, () =>
            {
                try
                {
                    _statistics.IncrementRemainingWorkload();
                    activationSuccessful = this.Post(new SuccessfulProcessingResult
                    {
                        NewResources = new List<Resource>
                        {
                            new Resource(_incrementalIdGenerator.GetNext(), startUrl, null, true)
                            {
                                IsInternal = true
                            }
                        }
                    });

                    if (!activationSuccessful) throw new ArgumentException("Could not activate workflow using given start URL", startUrl);
                }
                catch (Exception exception)
                {
                    _statistics.DecrementRemainingWorkload();
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

                var newlyDiscoveredResources = new List<Resource>();
                var processedResourceIsNotQueuedForReprocessing = true;
                var processedResource = processingResult.ProcessedResource;
                var isNotActivationProcessingResult = processedResource != null;
                if (isNotActivationProcessingResult)
                {
                    if (!_processedUrls.ContainsKey(processedResource.OriginalUri.AbsoluteUri))
                        _log.Error($"Processed resource was not registered by {nameof(CoordinatorBlock)}: {processedResource.ToJson()}");

                    if (processedResource.StatusCode.IsWithinBrokenRange()) _statistics.IncrementBrokenUrlCount();
                    else _statistics.IncrementValidUrlCount();

                    var redirectHappened = !processedResource.OriginalUri.Equals(processedResource.Uri);
                    switch (processingResult)
                    {
                        case FailedProcessingResult _:
                        {
                            if (redirectHappened)
                            {
                                if (RedirectHappenedAtStartUrl()) return null;
                                processedResourceIsNotQueuedForReprocessing = !TryQueueForReprocessing();
                            }
                            else
                            {
                                var verificationResult = processedResource.ToVerificationResult();
                                SendOutReportWritingMessage((ReportWritingAction.AddNew, verificationResult));
                            }
                            break;
                        }
                        case SuccessfulProcessingResult successfulProcessingResult:
                        {
                            var millisecondsPageLoadTime = successfulProcessingResult.MillisecondsPageLoadTime;
                            if (millisecondsPageLoadTime.HasValue)
                                _statistics.IncrementSuccessfullyRenderedPageCount(millisecondsPageLoadTime.Value);

                            var reportWritingAction = ReportWritingAction.AddNew;
                            var verificationResult = processedResource.ToVerificationResult();
                            if (redirectHappened && UriIsAlreadySavedToReport(verificationResult.VerifiedUrl))
                                reportWritingAction = ReportWritingAction.Update;

                            SendOutReportWritingMessage((reportWritingAction, verificationResult));
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException(nameof(processingResult));
                    }
                }

                newlyDiscoveredResources.AddRange(DiscoverNewResources());
                _statistics.DecrementRemainingWorkload();

                var statisticsSnapshot = _statistics.TakeSnapshot();
                var remainingWorkload = statisticsSnapshot.RemainingWorkload;
                if (processedResourceIsNotQueuedForReprocessing)
                {
                    if (isNotActivationProcessingResult)
                    {
                        SendOut(new ResourceProcessedEvent
                        {
                            RemainingWorkload = remainingWorkload,
                            ValidUrlCount = statisticsSnapshot.ValidUrlCount,
                            BrokenUrlCount = statisticsSnapshot.BrokenUrlCount,
                            VerifiedUrlCount = statisticsSnapshot.VerifiedUrlCount,
                            Message = $"{processedResource.StatusCode:D} - {processedResource.Uri.AbsoluteUri}",
                            MillisecondsAveragePageLoadTime = statisticsSnapshot.MillisecondsAveragePageLoadTime
                        });
                    }
                    else SendOut(new ResourceProcessedEvent { RemainingWorkload = remainingWorkload });
                }

                if (remainingWorkload > 0) return newlyDiscoveredResources;
                SendOut(new NoMoreWorkToDoEvent());
                return new List<Resource>();

                #region Local Functions

                bool UriIsAlreadySavedToReport(string url)
                {
                    if (_processedUrls.TryAdd(url, false))
                        return false;

                    _processedUrls.TryGetValue(url, out var urlIsAlreadySavedToReport);
                    return urlIsAlreadySavedToReport;
                }
                bool TryQueueForReprocessing()
                {
                    if (!_processedUrls.TryAdd(processedResource.Uri.AbsoluteUri, false))
                        return false;

                    newlyDiscoveredResources.Add(new Resource(
                        processedResource.Id,
                        processedResource.Uri.AbsoluteUri,
                        processedResource.ParentUri,
                        processedResource.IsExtractedFromHtmlDocument
                    ));
                    _statistics.IncrementRemainingWorkload();
                    return true;
                }
                void SendOut(Event @event)
                {
                    if (!Events.Post(@event) && !Events.Completion.IsCompleted)
                        _log.Error($"Failed to post data to buffer block named [{nameof(Events)}].");
                }
                bool RedirectHappenedAtStartUrl()
                {
                    if (!_resourceScope.IsStartUri(processedResource.OriginalUri)) return false;

                    _log.Error(
                        "Redirect happened. Please provide a start url without any redirect. " +
                        $"Or use this url: {processedResource.Uri.AbsoluteUri}"
                    );

                    SendOut(new RedirectHappenedAtStartUrlEvent());
                    return true;
                }
                void SendOutReportWritingMessage((ReportWritingAction, VerificationResult) reportWritingMessage)
                {
                    if (!ReportWritingMessages.Post(reportWritingMessage) && !ReportWritingMessages.Completion.IsCompleted)
                    {
                        _log.Error($"Failed to post data to buffer block named [{nameof(ReportWritingMessages)}].");
                        return;
                    }

                    var (reportWritingAction, verificationResult) = reportWritingMessage;
                    if (reportWritingAction == ReportWritingAction.AddNew)
                        _processedUrls[verificationResult.VerifiedUrl] = true;
                }
                List<Resource> DiscoverNewResources()
                {
                    if (!(processingResult is SuccessfulProcessingResult successfulProcessingResult)) return new List<Resource>();
                    return successfulProcessingResult.NewResources.Where(newResource =>
                    {
                        var resourceWasNotProcessed = _processedUrls.TryAdd(newResource.Uri.AbsoluteUri, false);
                        if (resourceWasNotProcessed) _statistics.IncrementRemainingWorkload();
                        return resourceWasNotProcessed;
                    }).ToList();
                }

                #endregion
            }
            catch (Exception exception)
            {
                _log.Error($"One or more errors occurred while coordinating: {JsonConvert.SerializeObject(processingResult)}.", exception);
                return null;
            }
        }

        #region Injected Services

        readonly ILog _log;
        readonly IStatistics _statistics;
        readonly IResourceScope _resourceScope;
        readonly IIncrementalIdGenerator _incrementalIdGenerator;

        #endregion
    }
}