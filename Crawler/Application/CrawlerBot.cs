using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Crawler
{
    public class CrawlerBot
    {
        IEventBroadcaster _eventBroadcaster;
        IHardwareMonitor _hardwareMonitor;
        readonly ILogger _logger;
        IMemory _memory;
        IReportWriter _reportWriter;
        IResourceEnricher _resourceEnricher;
        IResourceScope _resourceScope;
        IScheduler _scheduler;
        readonly StateMachine<CrawlerState, CrawlerCommand> _stateMachine;
        Task _waitingForCompletionTask;

        public IStatistics Statistics { get; private set; }

        public CrawlerState CrawlerState => _stateMachine.CurrentState;

        public int RemainingWorkload
        {
            get
            {
                try { return _scheduler?.RemainingWorkload ?? 0; }
                catch (ObjectDisposedException) { return 0; }
            }
        }

        public event Action<Event> OnEventBroadcast;

        public CrawlerBot()
        {
            _logger = ServiceLocator.Get<ILogger>();
            _stateMachine = new StateMachine<CrawlerState, CrawlerCommand>(
                new Dictionary<Transition<CrawlerState, CrawlerCommand>, CrawlerState>
                {
                    { CreateTransition(CrawlerState.WaitingForInitialization, CrawlerCommand.Stop), CrawlerState.Completed },
                    { CreateTransition(CrawlerState.WaitingForInitialization, CrawlerCommand.Initialize), CrawlerState.WaitingToRun },
                    { CreateTransition(CrawlerState.WaitingToRun, CrawlerCommand.Run), CrawlerState.Running },
                    { CreateTransition(CrawlerState.WaitingToRun, CrawlerCommand.Abort), CrawlerState.WaitingForStop },
                    { CreateTransition(CrawlerState.WaitingForStop, CrawlerCommand.Stop), CrawlerState.Completed },
                    { CreateTransition(CrawlerState.Running, CrawlerCommand.Stop), CrawlerState.Completed },
                    { CreateTransition(CrawlerState.Running, CrawlerCommand.Pause), CrawlerState.Paused },
                    { CreateTransition(CrawlerState.Completed, CrawlerCommand.MarkAsRanToCompletion), CrawlerState.RanToCompletion },
                    { CreateTransition(CrawlerState.Completed, CrawlerCommand.MarkAsCancelled), CrawlerState.Cancelled },
                    { CreateTransition(CrawlerState.Completed, CrawlerCommand.MarkAsFaulted), CrawlerState.Faulted },
                    { CreateTransition(CrawlerState.Paused, CrawlerCommand.Resume), CrawlerState.Running }
                },
                CrawlerState.WaitingForInitialization
            );

            Transition<CrawlerState, CrawlerCommand> CreateTransition(CrawlerState fromState, CrawlerCommand command)
            {
                return new Transition<CrawlerState, CrawlerCommand>(fromState, command);
            }
        }

        static CrawlerBot()
        {
            // TODO: A workaround for .Net Core 2.x bug. Should be removed in the future.
            AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
        }

        public void Stop()
        {
            if (!TryTransit(CrawlerCommand.Stop)) return;
            var crawlerCommand = CrawlerCommand.MarkAsCancelled;
            try
            {
                StopMonitoringHardwareResources();
                DeactivateMainWorkflow();
                WaitForBackgroundTaskToComplete();
                ReleaseResources();
                TryTransit(crawlerCommand);
                BroadcastEvent(new Event
                {
                    EventType = EventType.Stopped,
                    Message = Enum.GetName(typeof(CrawlerState), CrawlerState)
                });
            }
            catch (Exception exception) { _logger.LogException(exception); }

            void StopMonitoringHardwareResources()
            {
                if (_hardwareMonitor == null || !_hardwareMonitor.IsRunning) return;
                BroadcastEvent(StopProgressUpdatedEvent("Stopping hardware monitor service ..."));
                _hardwareMonitor.StopMonitoring();
            }
            void DeactivateMainWorkflow()
            {
                if (_scheduler == null) return;
                BroadcastEvent(StopProgressUpdatedEvent("De-activating main workflow ..."));

                if (_scheduler.RemainingWorkload == 0 && _memory.AlreadyVerifiedUrlCount > 0)
                    crawlerCommand = CrawlerCommand.MarkAsRanToCompletion;
                _scheduler.CancelPendingTasks();
            }
            void WaitForBackgroundTaskToComplete()
            {
                if (_waitingForCompletionTask == null) return;
                BroadcastEvent(StopProgressUpdatedEvent("Waiting for background tasks to complete ..."));

                try { _waitingForCompletionTask.Wait(); }
                catch (Exception exception)
                {
                    _logger.LogException(exception);
                    crawlerCommand = CrawlerCommand.MarkAsFaulted;
                }
                finally { _waitingForCompletionTask = null; }
            }
            void ReleaseResources()
            {
                BroadcastEvent(StopProgressUpdatedEvent("Releasing resources ..."));
                ServiceLocator.DisposeTransientServices();
            }
            Event StopProgressUpdatedEvent(string message = "")
            {
                return new Event
                {
                    EventType = EventType.StopProgressUpdated,
                    Message = message
                };
            }
        }

        public bool TryStart(Configurations configurations)
        {
            if (!TryTransit(CrawlerCommand.Initialize)) return false;
            try
            {
                ConnectServices();
                EnsureDirectoryContainsScreenshotFilesIsRecreated();
                ActivateMainWorkflow();
                MonitorHardwareResources();
                WaitForCompletionInSeparateThread();
                TryTransit(CrawlerCommand.Run);
                BroadcastEvent(new Event { EventType = EventType.ReportFileCreated });
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogException(exception);
                TryTransit(CrawlerCommand.Abort);
                _waitingForCompletionTask = Task.FromException(exception);

                Stop();
                return false;
            }

            void ConnectServices()
            {
                _logger.LogInfo("Connecting services ...");
                ServiceLocator.CreateTransientServices(configurations);
                Statistics = ServiceLocator.Get<IStatistics>();
                _memory = ServiceLocator.Get<IMemory>();
                _resourceScope = ServiceLocator.Get<IResourceScope>();
                _hardwareMonitor = ServiceLocator.Get<IHardwareMonitor>();
                _resourceEnricher = ServiceLocator.Get<IResourceEnricher>();
                _reportWriter = ServiceLocator.Get<IReportWriter>();
                _scheduler = ServiceLocator.Get<IScheduler>();

                _eventBroadcaster = ServiceLocator.Get<IEventBroadcaster>();
                _eventBroadcaster.OnEventBroadcast += BroadcastEvent;
            }
            void EnsureDirectoryContainsScreenshotFilesIsRecreated()
            {
                BroadcastEvent(StartProgressUpdatedEvent("Re-creating directory containing screenshot files ..."));
                if (Directory.Exists(Configurations.PathToDirectoryContainsScreenshotFiles))
                    Directory.Delete(Configurations.PathToDirectoryContainsScreenshotFiles, true);
                Directory.CreateDirectory(Configurations.PathToDirectoryContainsScreenshotFiles);
            }
            void ActivateMainWorkflow()
            {
                BroadcastEvent(StartProgressUpdatedEvent("Activating main workflow ..."));
                _memory.MemorizeToBeVerifiedResource(
                    _resourceEnricher.Enrich(new Resource
                    {
                        ParentUri = null,
                        OriginalUrl = configurations.StartUri.AbsoluteUri
                    })
                );
            }
            void MonitorHardwareResources()
            {
                _logger.LogInfo("Starting hardware monitor service ...");
                _hardwareMonitor.StartMonitoring();
            }
            void WaitForCompletionInSeparateThread()
            {
                _waitingForCompletionTask = Task.Run(() =>
                {
                    try
                    {
                        Task.WhenAll(
                            Task.Run(Render, _scheduler.CancellationToken),
                            Task.Run(Extract, _scheduler.CancellationToken),
                            Task.Run(Verify, _scheduler.CancellationToken)
                        ).Wait();
                    }
                    catch (Exception exception) { _logger.LogException(exception); }
                    finally
                    {
                        if (CrawlerState == CrawlerState.Running) Task.Run(Stop);
                    }
                }, _scheduler.CancellationToken);
            }
            Event StartProgressUpdatedEvent(string message)
            {
                return new Event
                {
                    EventType = EventType.StartProgressUpdated,
                    Message = message
                };
            }
        }

        void BroadcastEvent(Event @event)
        {
            OnEventBroadcast?.Invoke(@event);
            if (@event.EventType != EventType.ResourceVerified && !string.IsNullOrWhiteSpace(@event.Message))
                _logger.LogInfo(@event.Message);
        }

        void Extract()
        {
            while (_scheduler.RemainingWorkload != 0 && !_scheduler.CancellationToken.IsCancellationRequested)
                _scheduler.CreateTask((resourceExtractor, toBeExtractedHtmlDocument) =>
                {
                    resourceExtractor.ExtractResourcesFrom(
                        toBeExtractedHtmlDocument,
                        resource => _memory.MemorizeToBeVerifiedResource(resource)
                    );
                });
        }

        void Render()
        {
            while (_scheduler.RemainingWorkload != 0 && !_scheduler.CancellationToken.IsCancellationRequested)
                _scheduler.CreateTask((htmlRenderer, toBeRenderedResource) =>
                {
                    var renderingFailed = !htmlRenderer.TryRender(
                        toBeRenderedResource,
                        out var htmlText,
                        out var millisecondsPageLoadTime,
                        _scheduler.CancellationToken,
                        _logger.LogException
                    );
                    if (renderingFailed) return;
                    if (millisecondsPageLoadTime.HasValue)
                    {
                        Statistics.IncrementSuccessfullyRenderedPageCount();
                        Statistics.IncrementTotalPageLoadTimeBy(millisecondsPageLoadTime.Value);
                    }

                    if (toBeRenderedResource.IsBroken) return;
                    _memory.MemorizeToBeExtractedHtmlDocument(new HtmlDocument { Uri = toBeRenderedResource.Uri, Text = htmlText });
                });
        }

        bool TryTransit(CrawlerCommand crawlerCommand)
        {
            if (_stateMachine.TryMoveNext(crawlerCommand)) return true;

            var commandName = Enum.GetName(typeof(CrawlerCommand), crawlerCommand);
            _logger.LogInfo($"Transition from state [{_stateMachine.CurrentState}] via [{commandName}] command failed.\n" +
                            $"{Environment.StackTrace}");
            return false;
        }

        void Verify()
        {
            while (_scheduler.RemainingWorkload != 0 && !_scheduler.CancellationToken.IsCancellationRequested)
                _scheduler.CreateTask((resourceVerifier, resource) =>
                {
                    if (!resourceVerifier.TryVerify(resource, _scheduler.CancellationToken, out var verificationResult)) return;
                    var isOrphanedUri = verificationResult.StatusCode == StatusCode.OrphanedUri;
                    var uriSchemeNotSupported = verificationResult.StatusCode == StatusCode.UriSchemeNotSupported;
                    if (isOrphanedUri || uriSchemeNotSupported) return;
                    // TODO: We should log these orphaned uri-s somewhere

                    if (resource.IsBroken) Statistics.IncrementBrokenUrlCount();
                    else Statistics.IncrementValidUrlCount();

                    _reportWriter.WriteReport(verificationResult);
                    BroadcastEvent(new Event
                    {
                        EventType = EventType.ResourceVerified,
                        Message = $"{verificationResult.StatusCode:D} - {verificationResult.VerifiedUrl}"
                    });

                    var resourceSizeInMb = resource.Size / 1024f / 1024f;
                    var resourceIsTooBig = resourceSizeInMb > 10;
                    if (resourceIsTooBig)
                        _logger.LogInfo($"Resource was not queued for rendering because it was too big ({resourceSizeInMb} MB) - " +
                                        $"{resource.Uri}");

                    var isInternalResource = resource.IsInternal;
                    var isExtractedResource = resource.IsExtracted;
                    var isInitialResource = resource.Uri != null && _resourceScope.IsStartUri(resource.Uri);
                    var isNotStaticAsset = !ResourceType.StaticAsset.HasFlag(resource.ResourceType);
                    if (isInternalResource && isNotStaticAsset && !resourceIsTooBig && (isExtractedResource || isInitialResource))
                        _memory.MemorizeToBeRenderedResource(resource);
                });
        }
    }
}