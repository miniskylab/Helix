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
    public static class CrawlerBot
    {
        static IEventBroadcaster _eventBroadcaster;
        static IHardwareMonitor _hardwareMonitor;
        static IMemory _memory;
        static IReportWriter _reportWriter;
        static IResourceProcessor _resourceProcessor;
        static IResourceScope _resourceScope;
        static IScheduler _scheduler;
        static Task _waitingForCompletionTask;
        static readonly ILogger Logger;
        static readonly StateMachine<CrawlerState, CrawlerCommand> StateMachine;
        static readonly object TransitionLock;

        public static IStatistics Statistics { get; private set; }

        public static CrawlerState CrawlerState
        {
            get
            {
                lock (TransitionLock) return StateMachine.CurrentState;
            }
        }

        public static int RemainingWorkload
        {
            get
            {
                try { return _scheduler?.RemainingWorkload ?? 0; }
                catch (ObjectDisposedException) { return 0; }
            }
        }

        public static event Action<Event> OnEventBroadcast;

        static CrawlerBot()
        {
            // TODO: A workaround for .Net Core 2.x bug. Should be removed in the future.
            AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;

            TransitionLock = new object();
            Logger = ServiceLocator.Get<ILogger>();
            StateMachine = new StateMachine<CrawlerState, CrawlerCommand>(
                new Dictionary<Transition<CrawlerState, CrawlerCommand>, CrawlerState>
                {
                    { CreateTransition(CrawlerState.WaitingToRun, CrawlerCommand.StartWorking), CrawlerState.Running },
                    { CreateTransition(CrawlerState.WaitingToRun, CrawlerCommand.StopWorking), CrawlerState.Stopping },
                    { CreateTransition(CrawlerState.Running, CrawlerCommand.StopWorking), CrawlerState.Stopping },
                    { CreateTransition(CrawlerState.Running, CrawlerCommand.Pause), CrawlerState.Paused },
                    { CreateTransition(CrawlerState.Running, CrawlerCommand.MarkAsFaulted), CrawlerState.Faulted },
                    { CreateTransition(CrawlerState.Stopping, CrawlerCommand.MarkAsRanToCompletion), CrawlerState.RanToCompletion },
                    { CreateTransition(CrawlerState.Stopping, CrawlerCommand.MarkAsCancelled), CrawlerState.Cancelled },
                    { CreateTransition(CrawlerState.Stopping, CrawlerCommand.MarkAsFaulted), CrawlerState.Faulted },
                    { CreateTransition(CrawlerState.Faulted, CrawlerCommand.StartWorking), CrawlerState.Running },
                    { CreateTransition(CrawlerState.Faulted, CrawlerCommand.StopWorking), CrawlerState.Faulted },
                    { CreateTransition(CrawlerState.Paused, CrawlerCommand.Resume), CrawlerState.Running },
                    { CreateTransition(CrawlerState.RanToCompletion, CrawlerCommand.StartWorking), CrawlerState.Running },
                    { CreateTransition(CrawlerState.Cancelled, CrawlerCommand.StartWorking), CrawlerState.Running }
                },
                CrawlerState.WaitingToRun
            );

            Transition<CrawlerState, CrawlerCommand> CreateTransition(CrawlerState fromState, CrawlerCommand command)
            {
                return new Transition<CrawlerState, CrawlerCommand>(fromState, command);
            }
        }

        public static void StopWorking()
        {
            if (!TryTransit(CrawlerCommand.StopWorking)) return;
            try
            {
                BroadcastEvent(StopProgressUpdatedEvent("Initializing stop sequence ..."));
                if (_hardwareMonitor.IsRunning)
                {
                    BroadcastEvent(StopProgressUpdatedEvent("Stopping hardware monitor service ..."));
                    _hardwareMonitor.StopMonitoring();
                }

                var crawlerCommand = CrawlerCommand.MarkAsCancelled;
                if (_scheduler != null)
                {
                    BroadcastEvent(StopProgressUpdatedEvent("De-activating main workflow ..."));
                    if (_scheduler.RemainingWorkload == 0) crawlerCommand = CrawlerCommand.MarkAsRanToCompletion;
                    _scheduler.CancelEverything();

                    BroadcastEvent(StopProgressUpdatedEvent("Waiting for background tasks to complete ..."));
                    try
                    {
                        _waitingForCompletionTask?.Wait();
                        _waitingForCompletionTask = null;
                    }
                    catch (Exception exception)
                    {
                        Logger.LogException(exception);
                        crawlerCommand = CrawlerCommand.MarkAsFaulted;
                    }
                }

                BroadcastEvent(StopProgressUpdatedEvent("Releasing resources ..."));
                ServiceLocator.DisposeTransientServices();

                TryTransit(crawlerCommand);
                BroadcastEvent(new Event
                {
                    EventType = EventType.Stopped,
                    Message = Enum.GetName(typeof(CrawlerState), CrawlerState)
                });
                OnEventBroadcast = null;

                Event StopProgressUpdatedEvent(string message = "")
                {
                    return new Event
                    {
                        EventType = EventType.StopProgressUpdated,
                        Message = message
                    };
                }
            }
            catch (Exception exception) { Logger.LogException(exception); }
        }

        public static bool TryStartWorking(Configurations configurations)
        {
            if (!TryTransit(CrawlerCommand.StartWorking)) return false;
            try
            {
                Logger.LogInfo("Initializing start sequence ...");
                _resourceProcessor = null;
                _eventBroadcaster = null;
                _hardwareMonitor = null;
                _resourceScope = null;
                _reportWriter = null;
                _scheduler = null;
                _memory = null;

                Logger.LogInfo("Connecting services ...");
                ConnectServices();

                BroadcastEvent(StartProgressUpdatedEvent("Re-creating directory containing screenshot files ..."));
                EnsureDirectoryContainsScreenshotFilesIsRecreated();

                _waitingForCompletionTask = Task.Run(() =>
                {
                    try
                    {
                        Logger.LogInfo("Starting hardware monitor service ...");
                        _hardwareMonitor.StartMonitoring();

                        BroadcastEvent(StartProgressUpdatedEvent("Activating main workflow ..."));
                        ActivateMainWorkflowAndWaitForCompletion();
                    }
                    catch (Exception exception)
                    {
                        Logger.LogException(exception);
                        TryTransit(CrawlerCommand.MarkAsFaulted);
                    }
                    finally { Task.Run(StopWorking); }
                }, _scheduler.CancellationToken);
                return true;
            }
            catch (Exception exception)
            {
                Logger.LogException(exception);
                TryTransit(CrawlerCommand.MarkAsFaulted);
                StopWorking();
                return false;
            }

            void ConnectServices()
            {
                ServiceLocator.CreateTransientServices(configurations);
                Statistics = ServiceLocator.Get<IStatistics>();
                _memory = ServiceLocator.Get<IMemory>();
                _resourceScope = ServiceLocator.Get<IResourceScope>();
                _hardwareMonitor = ServiceLocator.Get<IHardwareMonitor>();
                _resourceProcessor = ServiceLocator.Get<IResourceProcessor>();
                _reportWriter = ServiceLocator.Get<IReportWriter>();
                _scheduler = ServiceLocator.Get<IScheduler>();

                _eventBroadcaster = ServiceLocator.Get<IEventBroadcaster>();
                _eventBroadcaster.OnEventBroadcast += BroadcastEvent;
            }
            void EnsureDirectoryContainsScreenshotFilesIsRecreated()
            {
                if (Directory.Exists(configurations.PathToDirectoryContainsScreenshotFiles))
                    Directory.Delete(configurations.PathToDirectoryContainsScreenshotFiles, true);
                Directory.CreateDirectory(configurations.PathToDirectoryContainsScreenshotFiles);
            }
            void ActivateMainWorkflowAndWaitForCompletion()
            {
                _memory.MemorizeToBeVerifiedResource(
                    _resourceProcessor.Enrich(new Resource
                    {
                        ParentUri = null,
                        OriginalUrl = configurations.StartUri.AbsoluteUri
                    })
                );
                Task.WhenAll(
                    Task.Run(Render, _scheduler.CancellationToken),
                    Task.Run(Extract, _scheduler.CancellationToken),
                    Task.Run(Verify, _scheduler.CancellationToken)
                ).Wait();
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

        static void BroadcastEvent(Event @event)
        {
            OnEventBroadcast?.Invoke(@event);
            if (@event.EventType != EventType.ResourceVerified && !string.IsNullOrWhiteSpace(@event.Message))
                Logger.LogInfo(@event.Message);
        }

        static void Extract()
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

        static void Render()
        {
            while (_scheduler.RemainingWorkload != 0 && !_scheduler.CancellationToken.IsCancellationRequested)
                _scheduler.CreateTask((htmlRenderer, toBeRenderedResource) =>
                {
                    var renderingFailed = !htmlRenderer.TryRender(
                        toBeRenderedResource,
                        out var htmlText,
                        out var millisecondsPageLoadTime,
                        _scheduler.CancellationToken,
                        Logger.LogException
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

        static bool TryTransit(CrawlerCommand crawlerCommand)
        {
            lock (TransitionLock)
            {
                if (!StateMachine.TryGetNext(crawlerCommand, out _))
                {
                    var commandName = Enum.GetName(typeof(CrawlerCommand), crawlerCommand);
                    Logger.LogInfo($"Transition from state [{StateMachine.CurrentState}] via [{commandName}] command failed.\n" +
                                   $"{Environment.StackTrace}");
                    return false;
                }
                StateMachine.MoveNext(crawlerCommand);
                return true;
            }
        }

        static void Verify()
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
                        Logger.LogInfo($"Resource was not queued for rendering because it was too big ({resourceSizeInMb} MB) - " +
                                       $"{resource.Uri}");

                    var isInternalResource = resource.IsInternal;
                    var isExtractedResource = resource.IsExtracted;
                    var isInitialResource = _resourceScope.IsStartUri(resource.Uri);
                    var isNotStaticAsset = !ResourceType.StaticAsset.HasFlag(resource.ResourceType);
                    if (isInternalResource && isNotStaticAsset && !resourceIsTooBig && (isExtractedResource || isInitialResource))
                        _memory.MemorizeToBeRenderedResource(resource);
                });
        }
    }
}