using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Helix.Core;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Crawler
{
    public static class CrawlerBot
    {
        static ILogger _logger;
        static IMemory _memory;
        static IReportWriter _reportWriter;
        static IScheduler _scheduler;
        static IServicePool _servicePool;
        static readonly StateMachine<CrawlerState, CrawlerCommand> _stateMachine;
        static readonly List<Task> BackgroundTasks;
        static readonly object TransitionLock;

        public static IStatistics Statistics { get; private set; }

        public static CrawlerState CrawlerState
        {
            get
            {
                lock (TransitionLock) return _stateMachine.CurrentState;
            }
        }

        public static int RemainingUrlCount
        {
            get
            {
                try { return _scheduler?.RemainingUrlCount ?? 0; }
                catch (ObjectDisposedException) { return 0; }
            }
        }

        public static event Action<VerificationResult> OnResourceVerified;
        public static event Action OnStopped;

        static CrawlerBot()
        {
            BackgroundTasks = new List<Task>();
            TransitionLock = new object();
            _stateMachine = new StateMachine<CrawlerState, CrawlerCommand>(
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
            ServiceLocator.AddSingleton(configurations);
            Statistics = ServiceLocator.Get<IStatistics>();
            _scheduler = ServiceLocator.Get<IScheduler>();
            _servicePool = ServiceLocator.Get<IServicePool>();
            _memory = ServiceLocator.Get<IMemory>();

            if (!TryTransit(CrawlerCommand.StartWorking)) return;
            BackgroundTasks.Add(Task.Run(() =>
            {
                try
                {
                    EnsureErrorLogFileIsRecreated();
                    _reportWriter = ServiceLocator.Get<IReportWriter>();
                    _servicePool.EnsureEnoughResources(_scheduler.CancellationToken);
                    _memory.Memorize(
                        new RawResource { ParentUri = null, Url = configurations.StartUri.AbsoluteUri },
                        _scheduler.CancellationToken
                    );

                    var renderingTask = Task.Run(Render, _scheduler.CancellationToken);
                    var extractionTask = Task.Run(Extract, _scheduler.CancellationToken);
                    var verificationTask = Task.Run(Verify, _scheduler.CancellationToken);
                    BackgroundTasks.Add(renderingTask);
                    BackgroundTasks.Add(extractionTask);
                    BackgroundTasks.Add(verificationTask);
                    Task.WhenAll(renderingTask, extractionTask, verificationTask).Wait();
                }
                catch (Exception exception)
                {
                    _logger.LogException(exception);
                    TryTransit(CrawlerCommand.MarkAsFaulted);
                }
                finally { Task.Run(StopWorking); }
            }, _scheduler.CancellationToken));

            void EnsureErrorLogFileIsRecreated()
            {
                _logger = ServiceLocator.Get<ILogger>();
                _logger.LogInfo("Started working ...");
            }
        }

        public static void StopWorking()
        {
            if (!TryTransit(CrawlerCommand.StopWorking)) return;
            var crawlerCommand = CrawlerCommand.MarkAsRanToCompletion;
            if (_scheduler == null || !_scheduler.EverythingIsDone) crawlerCommand = CrawlerCommand.MarkAsCancelled;

            _logger.LogInfo("Stopping ...");
            _scheduler?.CancelEverything();
            try { Task.WhenAll(BackgroundTasks).Wait(); }
            catch (Exception exception)
            {
                _logger.LogException(exception);
                crawlerCommand = CrawlerCommand.MarkAsFaulted;
            }

            ServiceLocator.Dispose();
            TryTransit(crawlerCommand);
            OnStopped?.Invoke();
        }

        static void Extract()
        {
            while (!_scheduler.EverythingIsDone && !_scheduler.CancellationToken.IsCancellationRequested)
                _scheduler.CreateTask((rawResourceExtractor, toBeExtractedHtmlDocument) =>
                {
                    rawResourceExtractor.ExtractRawResourcesFrom(
                        toBeExtractedHtmlDocument,
                        rawResource => _memory.Memorize(rawResource, _scheduler.CancellationToken)
                    );
                });
        }

        static void Render()
        {
            while (!_scheduler.EverythingIsDone && !_scheduler.CancellationToken.IsCancellationRequested)
                _scheduler.CreateTask((htmlRenderer, toBeRenderedResource) =>
                {
                    if (!htmlRenderer.TryRender(toBeRenderedResource, out var htmlText, out var pageLoadTime, _scheduler.CancellationToken,
                        onFailed: _logger.LogException)) return;

                    if (pageLoadTime.HasValue)
                    {
                        Statistics.SuccessfullyRenderedPageCount++;
                        Statistics.TotalPageLoadTime += pageLoadTime.Value;
                    }
                    else _logger.LogException(new InvalidConstraintException(ErrorMessage.SuccessfulRenderWithoutPageLoadTime));

                    _memory.Memorize(new HtmlDocument
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
                if (!_stateMachine.TryGetNext(crawlerCommand, out _)) return false;
                _stateMachine.MoveNext(crawlerCommand);
                return true;
            }
        }

        static void Verify()
        {
            while (!_scheduler.EverythingIsDone && !_scheduler.CancellationToken.IsCancellationRequested)
                _scheduler.CreateTask((rawResourceVerifier, toBeVerifiedRawResource) =>
                {
                    if (!rawResourceVerifier.TryVerify(toBeVerifiedRawResource, out var verificationResult)) return;
                    var isOrphanedUri = verificationResult.StatusCode == HttpStatusCode.OrphanedUri;
                    var uriSchemeNotSupported = verificationResult.StatusCode == HttpStatusCode.UriSchemeNotSupported;
                    if (isOrphanedUri || uriSchemeNotSupported) return;
                    // TODO: We should log these orphaned uri-s somewhere

                    _reportWriter.WriteReport(verificationResult);
                    Statistics.VerifiedUrlCount++;
                    if (verificationResult.IsBrokenResource) Statistics.BrokenUrlCount++;
                    else Statistics.ValidUrlCount++;
                    OnResourceVerified?.Invoke(verificationResult);

                    var resourceExists = verificationResult.Resource != null;
                    var isExtracted = verificationResult.IsExtractedResource;
                    var isInternal = verificationResult.IsInternalResource;
                    if (resourceExists && isExtracted && isInternal)
                        _memory.Memorize(verificationResult.Resource, _scheduler.CancellationToken);
                });
        }
    }
}