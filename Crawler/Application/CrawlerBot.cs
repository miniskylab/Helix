using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
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
        static readonly List<Task> BackgroundTasks;
        static readonly object CrawlerStateTransitionLock;

        public static CrawlerState CrawlerState { get; private set; } = CrawlerState.Ready;

        public static IStatistics Statistics { get; private set; }

        public static int RemainingUrlCount
        {
            get
            {
                try { return _scheduler?.RemainingUrlCount ?? 0; }
                catch (ObjectDisposedException) { return 0; }
            }
        }

        public static event Action<VerificationResult> OnResourceVerified;
        public static event Action<bool> OnStopped;

        static CrawlerBot()
        {
            BackgroundTasks = new List<Task>();
            CrawlerStateTransitionLock = new object();
        }

        public static void StartWorking(Configurations configurations)
        {
            ServiceLocator.AddSingleton(configurations);
            Statistics = ServiceLocator.Get<IStatistics>();
            _scheduler = ServiceLocator.Get<IScheduler>();
            _servicePool = ServiceLocator.Get<IServicePool>();
            _memory = ServiceLocator.Get<IMemory>();

            if (!TryTransitTo(CrawlerState.Working)) return;
            BackgroundTasks.Add(Task.Run(() =>
            {
                try
                {
                    EnsureErrorLogFileIsRecreated();
                    _reportWriter = ServiceLocator.Get<IReportWriter>();
                    _servicePool.EnsureEnoughResources(_scheduler.CancellationToken);

                    var renderingTask = Task.Run(Render, _scheduler.CancellationToken);
                    var extractionTask = Task.Run(Extract, _scheduler.CancellationToken);
                    var verificationTask = Task.Run(Verify, _scheduler.CancellationToken);
                    BackgroundTasks.Add(renderingTask);
                    BackgroundTasks.Add(extractionTask);
                    BackgroundTasks.Add(verificationTask);
                    Task.WhenAll(renderingTask, extractionTask, verificationTask).Wait();
                }
                catch (Exception exception) { _logger.LogException(exception); }
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
            if (_scheduler != null && !TryTransitTo(CrawlerState.Stopping)) return;
            var everythingIsDone = _scheduler?.EverythingIsDone ?? false;

            _logger.LogInfo("Stopping ...");
            _scheduler?.CancelEverything();
            try { Task.WhenAll(BackgroundTasks).Wait(); }
            catch (Exception exception) { _logger.LogException(exception); }

            ServiceLocator.Dispose();
            TryTransitTo(CrawlerState.Ready);
            OnStopped?.Invoke(everythingIsDone);
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
                    Action<Exception> onFailed = _logger.LogException;
                    if (!htmlRenderer.TryRender(toBeRenderedResource, onFailed, _scheduler.CancellationToken, out var htmlText,
                        out var pageLoadTime)) return;

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

        static bool TryTransitTo(CrawlerState crawlerState)
        {
            if (CrawlerState == CrawlerState.Unknown) return false;
            switch (crawlerState)
            {
                case CrawlerState.Ready:
                    lock (CrawlerStateTransitionLock)
                    {
                        if (CrawlerState != CrawlerState.Stopping) return false;
                        CrawlerState = CrawlerState.Ready;
                        return true;
                    }
                case CrawlerState.Working:
                    lock (CrawlerStateTransitionLock)
                    {
                        if (CrawlerState != CrawlerState.Ready && CrawlerState != CrawlerState.Paused) return false;
                        CrawlerState = CrawlerState.Working;
                        return true;
                    }
                case CrawlerState.Stopping:
                    lock (CrawlerStateTransitionLock)
                    {
                        if (CrawlerState != CrawlerState.Working && CrawlerState != CrawlerState.Paused) return false;
                        CrawlerState = CrawlerState.Stopping;
                        return true;
                    }
                case CrawlerState.Paused:
                    lock (CrawlerStateTransitionLock)
                    {
                        if (CrawlerState != CrawlerState.Working) return false;
                        CrawlerState = CrawlerState.Paused;
                        return true;
                    }
                case CrawlerState.Unknown:
                    throw new NotSupportedException($"Cannot transit to [{nameof(CrawlerState.Unknown)}] state.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(crawlerState), crawlerState, null);
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
                    var isNotBroken = !verificationResult.IsBrokenResource;
                    var isInternal = verificationResult.IsInternalResource;
                    if (resourceExists && isExtracted && isNotBroken && isInternal)
                        _memory.Memorize(verificationResult.Resource, _scheduler.CancellationToken);
                });
        }
    }
}