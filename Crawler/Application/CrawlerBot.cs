using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;
using Helix.WebBrowser.Abstractions;

namespace Helix.Crawler
{
    public static class CrawlerBot
    {
        static IEventBroadcaster _eventBroadcaster;
        static ILogger _logger;
        static IMemory _memory;
        static IReportWriter _reportWriter;
        static IResourceProcessor _resourceProcessor;
        static IResourceScope _resourceScope;
        static IScheduler _scheduler;
        static IServicePool _servicePool;
        static readonly List<Task> BackgroundTasks;
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

        public static event Action<Event> EventBroadcast;

        static CrawlerBot()
        {
            // TODO: A workaround for .Net Core 2.x bug. Should be removed in the future.
            AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;

            if (ServiceLocator.TryCreateSupportServices())
            {
                _logger = ServiceLocator.Get<ILogger>();
                _eventBroadcaster = ServiceLocator.Get<IEventBroadcaster>();
            }

            BackgroundTasks = new List<Task>();
            TransitionLock = new object();
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

        public static void StartWorking(Configurations configurations)
        {
            if (!TryTransit(CrawlerCommand.StartWorking)) return;
            if (ServiceLocator.TryCreateSupportServices())
            {
                _logger = ServiceLocator.Get<ILogger>();
                _eventBroadcaster = ServiceLocator.Get<IEventBroadcaster>();
            }

            _eventBroadcaster.OnEventBroadcast += OnEventBroadcast;
            _eventBroadcaster.Broadcast(Event("Initializing start sequence ..."));
            _eventBroadcaster.Broadcast(Event("Extracting [User-Agent] string from web browser ..."));
            var webBrowser = ServiceLocator.Get<IWebBrowserProvider>().GetWebBrowser(
                configurations.PathToChromiumExecutable,
                configurations.WorkingDirectory
            );
            configurations.UserAgent = webBrowser.GetUserAgentString();
            webBrowser.Dispose();

            _eventBroadcaster.Broadcast(Event("Connecting services ..."));
            ServiceLocator.CreateSessionScopedServices(configurations);
            Statistics = ServiceLocator.Get<IStatistics>();
            _memory = ServiceLocator.Get<IMemory>();
            _scheduler = ServiceLocator.Get<IScheduler>();
            _servicePool = ServiceLocator.Get<IServicePool>();
            _resourceScope = ServiceLocator.Get<IResourceScope>();
            _resourceProcessor = ServiceLocator.Get<IResourceProcessor>();

            BackgroundTasks.Add(Task.Run(() =>
            {
                try
                {
                    _eventBroadcaster.Broadcast(Event("Re-creating directory containing screenshot files ..."));
                    EnsureDirectoryContainsScreenshotFilesIsRecreated();

                    _eventBroadcaster.Broadcast(Event("Re-creating report database ..."));
                    _reportWriter = ServiceLocator.Get<IReportWriter>();

                    _eventBroadcaster.Broadcast(Event("Pre-creating services ..."));
                    _servicePool.PreCreateServices(_scheduler.CancellationToken);

                    _eventBroadcaster.Broadcast(Event("Activating main workflow ..."));
                    _memory.MemorizeToBeVerifiedResource(
                        _resourceProcessor.Enrich(new Resource
                        {
                            ParentUri = null,
                            OriginalUrl = configurations.StartUri.AbsoluteUri
                        }),
                        _scheduler.CancellationToken
                    );
                    var renderingTask = Task.Run(Render, _scheduler.CancellationToken);
                    var extractionTask = Task.Run(Extract, _scheduler.CancellationToken);
                    var verificationTask = Task.Run(Verify, _scheduler.CancellationToken);
                    BackgroundTasks.Add(renderingTask);
                    BackgroundTasks.Add(extractionTask);
                    BackgroundTasks.Add(verificationTask);

                    _logger.LogInfo("Working ...");
                    Task.WhenAll(renderingTask, extractionTask, verificationTask).Wait();
                }
                catch (Exception exception)
                {
                    _logger.LogException(exception);
                    TryTransit(CrawlerCommand.MarkAsFaulted);
                }
                finally { Task.Run(StopWorking); }
            }, _scheduler.CancellationToken));

            void EnsureDirectoryContainsScreenshotFilesIsRecreated()
            {
                if (Directory.Exists(configurations.PathToDirectoryContainsScreenshotFiles))
                    Directory.Delete(configurations.PathToDirectoryContainsScreenshotFiles, true);
                Directory.CreateDirectory(configurations.PathToDirectoryContainsScreenshotFiles);
            }
            Event Event(string message)
            {
                return new Event
                {
                    EventType = EventType.StartProgressUpdated,
                    Message = message
                };
            }
        }

        public static void StopWorking()
        {
            if (!TryTransit(CrawlerCommand.StopWorking)) return;
            _eventBroadcaster.Broadcast(Event("Initializing stop sequence ..."));

            var crawlerCommand = CrawlerCommand.MarkAsCancelled;
            if (_scheduler != null)
            {
                _eventBroadcaster.Broadcast(Event("De-activating main workflow ..."));
                _scheduler.CancelEverything();
                if (_scheduler.EverythingIsDone) crawlerCommand = CrawlerCommand.MarkAsRanToCompletion;

                _eventBroadcaster.Broadcast(Event("Waiting for background tasks to complete ..."));
                try { Task.WhenAll(BackgroundTasks).Wait(); }
                catch (Exception exception)
                {
                    _logger.LogException(exception);
                    crawlerCommand = CrawlerCommand.MarkAsFaulted;
                }
            }

            _eventBroadcaster.Broadcast(Event("Releasing resources ..."));
            ServiceLocator.DisposeSessionScopedAndSupportServices();

            TryTransit(crawlerCommand);
            _eventBroadcaster.Broadcast(new Event
            {
                EventType = EventType.Stopped,
                Message = GetStopEventMessage()
            });
            _eventBroadcaster.OnEventBroadcast -= OnEventBroadcast;
            EventBroadcast = null;

            string GetStopEventMessage()
            {
                switch (CrawlerState)
                {
                    case CrawlerState.RanToCompletion:
                        return "Done.";
                    case CrawlerState.Cancelled:
                        return "Cancelled.";
                    case CrawlerState.Faulted:
                        return "One or more errors occurred. Check the logs for more details.";
                    default:
                        throw new InvalidConstraintException();
                }
            }
            Event Event(string message = "")
            {
                return new Event
                {
                    EventType = EventType.StopProgressUpdated,
                    Message = message
                };
            }
        }

        static void Extract()
        {
            while (!_scheduler.EverythingIsDone && !_scheduler.CancellationToken.IsCancellationRequested)
                _scheduler.CreateTask((resourceExtractor, toBeExtractedHtmlDocument) =>
                {
                    resourceExtractor.ExtractResourcesFrom(
                        toBeExtractedHtmlDocument,
                        resource => _memory.MemorizeToBeVerifiedResource(resource, _scheduler.CancellationToken)
                    );
                });
        }

        static void OnEventBroadcast(Event @event)
        {
            EventBroadcast?.Invoke(@event);
            if (@event.EventType != EventType.ResourceVerified && !string.IsNullOrWhiteSpace(@event.Message))
                _logger.LogInfo(@event.Message);
        }

        static void Render()
        {
            while (!_scheduler.EverythingIsDone && !_scheduler.CancellationToken.IsCancellationRequested)
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
                    _memory.MemorizeToBeExtractedHtmlDocument(new HtmlDocument
                    {
                        Uri = toBeRenderedResource.Uri,
                        Text = htmlText
                    }, _scheduler.CancellationToken);
                });
        }

        static bool TryTransit(CrawlerCommand crawlerCommand)
        {
            lock (TransitionLock)
            {
                if (!StateMachine.TryGetNext(crawlerCommand, out _)) return false;
                StateMachine.MoveNext(crawlerCommand);
                return true;
            }
        }

        static void Verify()
        {
            while (!_scheduler.EverythingIsDone && !_scheduler.CancellationToken.IsCancellationRequested)
                _scheduler.CreateTask((resourceVerifier, resource) =>
                {
                    if (!resourceVerifier.TryVerify(resource, out var verificationResult)) return;
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
                        _logger.LogInfo($"Resource was not queued for rendering because it was too big ({resourceSizeInMb} MB) - " +
                                        $"{resource.Uri}");

                    var isInternalResource = resource.IsInternal;
                    var isExtractedResource = resource.IsExtracted;
                    var isInitialResource = _resourceScope.IsStartUri(resource.Uri);
                    var isNotStaticAsset = !ResourceType.StaticAsset.HasFlag(resource.ResourceType);
                    if (isInternalResource && isNotStaticAsset && !resourceIsTooBig && (isExtractedResource || isInitialResource))
                        _memory.MemorizeToBeRenderedResource(resource, _scheduler.CancellationToken);
                });
        }
    }
}