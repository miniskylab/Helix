using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;
using log4net;

namespace Helix.Crawler
{
    public class CrawlerBot
    {
        IEventBroadcaster _eventBroadcaster;
        IHardwareMonitor _hardwareMonitor;
        readonly ILog _log;
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
            _log = ServiceLocator.Get<ILog>();
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
                _eventBroadcaster.Broadcast(new Event
                {
                    EventType = EventType.Stopped,
                    Message = Enum.GetName(typeof(CrawlerState), CrawlerState)
                });
            }
            catch (Exception exception) when (!exception.IsAcknowledgingOperationCancelledException(_scheduler.CancellationToken))
            {
                _log.Error("One or more errors occured when stopping crawling.", exception);
            }

            void StopMonitoringHardwareResources()
            {
                if (_hardwareMonitor == null || !_hardwareMonitor.IsRunning) return;
                _eventBroadcaster.Broadcast(StopProgressUpdatedEvent("Stopping hardware monitor service ..."));
                _hardwareMonitor.StopMonitoring();
            }
            void DeactivateMainWorkflow()
            {
                if (_scheduler == null) return;
                _eventBroadcaster.Broadcast(StopProgressUpdatedEvent("De-activating main workflow ..."));

                if (_scheduler.RemainingWorkload == 0 && _memory.AlreadyVerifiedUrlCount > 0)
                    crawlerCommand = CrawlerCommand.MarkAsRanToCompletion;
                _scheduler.CancelPendingTasks();
            }
            void WaitForBackgroundTaskToComplete()
            {
                if (_waitingForCompletionTask == null) return;
                _eventBroadcaster.Broadcast(StopProgressUpdatedEvent("Waiting for background tasks to complete ..."));

                try { _waitingForCompletionTask.Wait(); }
                catch (Exception)
                {
                    crawlerCommand = CrawlerCommand.MarkAsFaulted;
                    throw;
                }
                finally { _waitingForCompletionTask = null; }
            }
            void ReleaseResources()
            {
                _eventBroadcaster.Broadcast(StopProgressUpdatedEvent("Disposing services ..."));
                ServiceLocator.DisposeServices();
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
                InitializeAndConnectServices();
                EnsureDirectoryContainsScreenshotFilesIsRecreated();
                MonitorHardwareResources();
                ActivateMainWorkflow();
                TryTransit(CrawlerCommand.Run);
                return true;
            }
            catch (Exception exception) when (!exception.IsAcknowledgingOperationCancelledException(_scheduler.CancellationToken))
            {
                _log.Error("One or more errors occured when trying to start crawling.", exception);
                TryTransit(CrawlerCommand.Abort);
                _waitingForCompletionTask = Task.FromException(exception);

                Stop();
                return false;
            }

            void InitializeAndConnectServices()
            {
                _log.Info("Initializing services ...");
                ServiceLocator.InitializeServices(configurations);

                _eventBroadcaster = ServiceLocator.Get<IEventBroadcaster>();
                _eventBroadcaster.OnEventBroadcast += @event =>
                {
                    OnEventBroadcast?.Invoke(@event);
                    if (@event.EventType != EventType.ResourceVerified && !string.IsNullOrWhiteSpace(@event.Message))
                        _log.Info(@event.Message);
                };
                _eventBroadcaster.Broadcast(StartProgressUpdatedEvent("Connecting services ..."));

                Statistics = ServiceLocator.Get<IStatistics>();
                _memory = ServiceLocator.Get<IMemory>();
                _resourceScope = ServiceLocator.Get<IResourceScope>();
                _hardwareMonitor = ServiceLocator.Get<IHardwareMonitor>();
                _resourceEnricher = ServiceLocator.Get<IResourceEnricher>();
                _reportWriter = ServiceLocator.Get<IReportWriter>();
                _scheduler = ServiceLocator.Get<IScheduler>();
            }
            void EnsureDirectoryContainsScreenshotFilesIsRecreated()
            {
                _eventBroadcaster.Broadcast(StartProgressUpdatedEvent("Re-creating directory containing screenshot files ..."));
                if (Directory.Exists(Configurations.PathToDirectoryContainsScreenshotFiles))
                    Directory.Delete(Configurations.PathToDirectoryContainsScreenshotFiles, true);
                Directory.CreateDirectory(Configurations.PathToDirectoryContainsScreenshotFiles);
            }
            void ActivateMainWorkflow()
            {
                _eventBroadcaster.Broadcast(StartProgressUpdatedEvent("Activating main workflow ..."));
                _memory.MemorizeToBeVerifiedResource(
                    _resourceEnricher.Enrich(new Resource
                    {
                        ParentUri = null,
                        OriginalUrl = configurations.StartUri.AbsoluteUri
                    })
                );
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
                    catch (Exception exception) when (!exception.IsAcknowledgingOperationCancelledException(_scheduler.CancellationToken))
                    {
                        _log.Error("One or more errors occured when activating main workflow", exception);
                    }
                    finally
                    {
                        if (CrawlerState == CrawlerState.Running) Task.Run(Stop);
                    }
                }, _scheduler.CancellationToken);

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
                                exception => { _log.Error("One or more errors occured when trying to render an URL.", exception); }
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
                            _eventBroadcaster.Broadcast(new Event
                            {
                                EventType = EventType.ResourceVerified,
                                Message = $"{verificationResult.StatusCode:D} - {verificationResult.VerifiedUrl}"
                            });

                            var resourceSizeInMb = resource.Size / 1024f / 1024f;
                            var resourceIsTooBig = resourceSizeInMb > 10;
                            if (resourceIsTooBig)
                                _log.Info($"Resource was not queued for rendering because it was too big ({resourceSizeInMb} MB) - " +
                                          $"{resource.Uri}");

                            var isInternalResource = resource.IsInternal;
                            var isExtractedResource = resource.IsExtracted;
                            var isInitialResource = resource.Uri != null && _resourceScope.IsStartUri(resource.Uri);
                            var isHtml = resource.ResourceType == ResourceType.Html;
                            if (isInternalResource && isHtml && !resourceIsTooBig && (isExtractedResource || isInitialResource))
                                _memory.MemorizeToBeRenderedResource(resource);
                        });
                }
            }
            void MonitorHardwareResources()
            {
                _log.Info("Starting hardware monitor service ...");
                _hardwareMonitor.StartMonitoring();
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

        bool TryTransit(CrawlerCommand crawlerCommand)
        {
            if (_stateMachine.TryMoveNext(crawlerCommand)) return true;

            var commandName = Enum.GetName(typeof(CrawlerCommand), crawlerCommand);
            _log.Info($"Transition from state [{_stateMachine.CurrentState}] via [{commandName}] command failed.\n" +
                      $"{Environment.StackTrace}");
            return false;
        }
    }
}