using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
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
        readonly ConcurrentDictionary<string, StatusCode?> _processedUrls;
        readonly StateMachine<WorkflowState, WorkflowCommand> _stateMachine;

        public BufferBlock<Event> Events { get; }

        public BufferBlock<(ReportWritingAction, VerificationResult[])> ReportWritingMessages { get; }

        public override Task Completion => Task.WhenAll(base.Completion, ReportWritingMessages.Completion, Events.Completion);

        public CoordinatorBlock(Configurations configurations, IStatistics statistics, IResourceScope resourceScope, ILog log,
            IIncrementalIdGenerator incrementalIdGenerator)
        {
            _log = log;
            _statistics = statistics;
            _resourceScope = resourceScope;
            _configurations = configurations;
            _incrementalIdGenerator = incrementalIdGenerator;
            _processedUrls = new ConcurrentDictionary<string, StatusCode?>();

            _stateMachine = NewStateMachine();
            Events = new BufferBlock<Event>(new DataflowBlockOptions { EnsureOrdered = true });
            ReportWritingMessages = new BufferBlock<(ReportWritingAction, VerificationResult[])>();

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

                var newResources = new List<Resource>();
                var processedResourceIsQueuedForReprocessing = false;
                var processedResource = processingResult.ProcessedResource;
                if (!TryProcessInput()) return null;

                newResources.AddRange(PreprocessNewResources());
                _statistics.DecrementRemainingWorkload();

                var statisticsSnapshot = _statistics.TakeSnapshot();
                UpdateGui();

                if (statisticsSnapshot.RemainingWorkload > 0)
                    return newResources;

                SendOut(new NoMoreWorkToDoEvent());
                return new List<Resource>();

                #region Local Functions

                void UpdateGui()
                {
                    if (processedResourceIsQueuedForReprocessing)
                        return;

                    var isActivationProcessingResult = processedResource == null;
                    if (isActivationProcessingResult)
                        SendOut(new ResourceProcessedEvent { RemainingWorkload = statisticsSnapshot.RemainingWorkload });
                    else
                        SendOut(new ResourceProcessedEvent
                        {
                            RemainingWorkload = statisticsSnapshot.RemainingWorkload,
                            ValidUrlCount = statisticsSnapshot.ValidUrlCount,
                            BrokenUrlCount = statisticsSnapshot.BrokenUrlCount,
                            VerifiedUrlCount = statisticsSnapshot.VerifiedUrlCount,
                            Message = $"{processedResource.StatusCode:D} - {processedResource.Uri.AbsoluteUri}",
                            MillisecondsAveragePageLoadTime = statisticsSnapshot.MillisecondsAveragePageLoadTime
                        });
                }
                bool TryProcessInput()
                {
                    var isActivationProcessingResult = processedResource == null;
                    if (isActivationProcessingResult) return true;

                    if (!_processedUrls.ContainsKey(processedResource.OriginalUri.AbsoluteUri))
                        _log.Error($"Processed resource was not registered by {nameof(CoordinatorBlock)}: {processedResource.ToJson()}");

                    var reportWritingAction = ReportWritingAction.AddNew;
                    var verificationResult = processedResource.ToVerificationResult();
                    switch (processingResult)
                    {
                        case FailedProcessingResult _:
                        {
                            var redirectHappened = !processedResource.OriginalUri.Equals(processedResource.Uri);
                            if (redirectHappened)
                            {
                                if (RedirectHappenedAtStartUrl()) return false;
                                processedResourceIsQueuedForReprocessing = TryQueueForReprocessing();
                            }
                            else WriteToReportFile(reportWritingAction, verificationResult);

                            break;
                        }
                        case SuccessfulProcessingResult successfulProcessingResult:
                        {
                            var millisecondsPageLoadTime = successfulProcessingResult.MillisecondsPageLoadTime;
                            if (millisecondsPageLoadTime.HasValue)
                                _statistics.IncrementSuccessfullyRenderedPageCount(millisecondsPageLoadTime.Value);

                            if (AlreadySavedToReportFile(verificationResult.VerifiedUrl)) reportWritingAction = ReportWritingAction.Update;
                            WriteToReportFile(reportWritingAction, verificationResult);

                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException(nameof(processingResult));
                    }

                    return true;

                    #region Local Functions

                    bool TryQueueForReprocessing()
                    {
                        if (!_processedUrls.TryAdd(processedResource.Uri.AbsoluteUri, null))
                            return false;

                        newResources.Add(new Resource(
                            processedResource.Id,
                            processedResource.Uri.AbsoluteUri,
                            processedResource.ParentUri,
                            processedResource.IsExtractedFromHtmlDocument
                        ));
                        _statistics.IncrementRemainingWorkload();
                        return true;
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

                    #endregion
                }
                void SendOut(Event @event)
                {
                    if (!Events.Post(@event) && !Events.Completion.IsCompleted)
                        _log.Error($"Failed to post data to buffer block named [{nameof(Events)}].");
                }
                List<Resource> PreprocessNewResources()
                {
                    if (!(processingResult is SuccessfulProcessingResult successfulProcessingResult)) return new List<Resource>();
                    return successfulProcessingResult.NewResources.Where(newResource =>
                    {
                        switch (newResource.StatusCode)
                        {
                            case StatusCode.MalformedUri:
                            case StatusCode.UriSchemeNotSupported when _configurations.IncludeNonHttpUrlsInReport:
                            {
                                var resourceWasProcessed = !_processedUrls.TryAdd(newResource.OriginalUrl, null);
                                if (resourceWasProcessed) return false;

                                var reportWritingAction = ReportWritingAction.AddNew;
                                var verificationResult = newResource.ToVerificationResult();
                                if (AlreadySavedToReportFile(newResource.OriginalUrl))
                                    reportWritingAction = ReportWritingAction.Update;

                                WriteToReportFile(reportWritingAction, verificationResult);
                                return false;
                            }
                            case StatusCode.UriSchemeNotSupported when !_configurations.IncludeNonHttpUrlsInReport: return false;
                            default:
                            {
                                var resourceWasNotProcessed = _processedUrls.TryAdd(newResource.Uri.AbsoluteUri, null);
                                if (resourceWasNotProcessed) _statistics.IncrementRemainingWorkload();
                                return resourceWasNotProcessed;
                            }
                        }
                    }).ToList();
                }
                bool AlreadySavedToReportFile(string verifiedUrl)
                {
                    if (_processedUrls.TryAdd(verifiedUrl, null))
                        return false;

                    _processedUrls.TryGetValue(verifiedUrl, out var statusCode);
                    return statusCode != null;
                }
                void WriteToReportFile(ReportWritingAction reportWritingAction, VerificationResult verificationResult)
                {
                    var reportWritingMessage = (reportWritingAction, new[] { verificationResult });
                    if (!ReportWritingMessages.Post(reportWritingMessage) && !ReportWritingMessages.Completion.IsCompleted)
                    {
                        _log.Error($"Failed to post data to buffer block named [{nameof(ReportWritingMessages)}].");
                        return;
                    }

                    CountVerifiedUrl();
                    _processedUrls[verificationResult.VerifiedUrl] = verificationResult.StatusCode;

                    #region Local Functions

                    void CountVerifiedUrl()
                    {
                        if (reportWritingAction == ReportWritingAction.Update)
                        {
                            _processedUrls.TryGetValue(verificationResult.VerifiedUrl, out var statusCodeFromReport);
                            if (statusCodeFromReport == null)
                                throw new InvalidConstraintException("Could not retrieve status code of url from report");

                            var oldStatusCode = statusCodeFromReport.Value;
                            var newStatusCode = processedResource.StatusCode;
                            if (newStatusCode == oldStatusCode) return;

                            if (oldStatusCode.IsWithinBrokenRange() && !newStatusCode.IsWithinBrokenRange())
                            {
                                _statistics.IncrementValidUrlCount();
                                _statistics.DecrementBrokenUrlCount();
                            }
                            else if (!oldStatusCode.IsWithinBrokenRange() && newStatusCode.IsWithinBrokenRange())
                            {
                                _statistics.IncrementBrokenUrlCount();
                                _statistics.DecrementValidUrlCount();
                            }
                        }
                        else
                        {
                            if (processedResource.StatusCode.IsWithinBrokenRange()) _statistics.IncrementBrokenUrlCount();
                            else _statistics.IncrementValidUrlCount();
                        }
                    }

                    #endregion
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
        readonly Configurations _configurations;
        readonly IIncrementalIdGenerator _incrementalIdGenerator;

        #endregion
    }
}