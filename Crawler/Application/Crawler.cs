using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Helix.Abstractions;

namespace Helix.Implementations
{
    public static class Crawler
    {
        public static IMemory Memory = ServiceLocator.Get<IMemory>();
        static Task _mainWorkingTask;
        static readonly BlockingCollection<IResourceCollector> ResourceCollectorPool = new BlockingCollection<IResourceCollector>();
        static readonly BlockingCollection<IResourceVerifier> ResourceVerifierPool = new BlockingCollection<IResourceVerifier>();

        public static event ExceptionOccurredEvent OnExceptionOccurred;
        public static event ResourceVerifiedEvent OnResourceVerified;
        public static event StoppedEvent OnStopped;
        public static event WebBrowserClosedEvent OnWebBrowserClosed;
        public static event WebBrowserOpenedEvent OnWebBrowserOpened;

        public static void Dispose()
        {
            StopWorking();
            ResourceCollectorPool?.Dispose();
            ResourceVerifierPool?.Dispose();
        }

        public static void StartWorking(Configurations configurations)
        {
            EnsureErrorLogFileIsDeleted();
            ServiceLocator.RegisterServices(configurations);
            Memory = ServiceLocator.Get<IMemory>();

            if (!Memory.TryTransitTo(CrawlerState.Working)) return;
            _mainWorkingTask = Task.Run(() =>
            {
                try
                {
                    InitializeResourceCollectorPool();
                    InitializeResourceVerifierPool();
                    Crawl();
                }
                catch (Exception exception) { HandleException(exception); }
                finally { Task.Run(StopWorking); }
            }, Memory.CancellationToken);
        }

        public static void StopWorking()
        {
            if (!Memory.TryTransitTo(CrawlerState.Stopping)) return;
            Memory.CancellationTokenSource.Cancel();
            _mainWorkingTask.Wait();
            while (ResourceVerifierPool.Any()) ResourceVerifierPool.Take().Dispose();
            while (ResourceCollectorPool.Any())
            {
                ResourceCollectorPool.Take().Dispose();
                OnWebBrowserClosed?.Invoke(Memory.Configurations.WebBrowserCount - ResourceCollectorPool.Count);
            }
            Memory.ForgetAllBackgroundCrawlingTasks();
            ReportWriter.Instance.Dispose();
            ServiceLocator.Dispose();
            Memory.TryTransitTo(CrawlerState.Ready);
            OnStopped?.Invoke(Memory.IsAllWorkDone);
        }

        static void Crawl()
        {
            while (Memory.TryTakeToBeVerifiedRawResource(out var rawResource) || !Memory.AllBackgroundCrawlingTasksAreDone)
            {
                if (Memory.CancellationToken.IsCancellationRequested) break;
                if (rawResource == null)
                {
                    Thread.Sleep(100);
                    continue;
                }

                var toBeVerifiedRawResource = rawResource;
                Task backgroundCrawlingTask = null;
                backgroundCrawlingTask = new Task(() =>
                {
                    try
                    {
                        if (Memory.CancellationToken.IsCancellationRequested) return;
                        var verificationResult = ResourceVerifierPool.Take(Memory.CancellationToken).Verify(toBeVerifiedRawResource);
                        if (verificationResult.HttpStatusCode != (int) HttpStatusCode.ExpectationFailed)
                        {
                            ReportWriter.Instance.WriteReport(verificationResult, Memory.Configurations.ReportBrokenLinksOnly);
                            OnResourceVerified?.Invoke(verificationResult);
                        }

                        var resourceIsCrawlable = toBeVerifiedRawResource.HttpStatusCode == 0;
                        if (!verificationResult.IsBrokenResource && verificationResult.IsInternalResource && resourceIsCrawlable)
                            ResourceCollectorPool.Take(Memory.CancellationToken).CollectNewRawResourcesFrom(verificationResult.Resource);
                    }
                    catch (Exception exception) { HandleException(exception); }
                    finally
                    {
                        // ReSharper disable once AccessToModifiedClosure
                        Memory.Forget(backgroundCrawlingTask);
                    }
                }, Memory.CancellationToken);
                Memory.Memorize(backgroundCrawlingTask);
                backgroundCrawlingTask.Start(TaskScheduler.Default);
            }
            while (Memory.BackgroundCrawlingTasks.Any(t => t.Status == TaskStatus.Running)) Thread.Sleep(500);
            Task.WhenAll(Memory.BackgroundCrawlingTasks).Wait();
        }

        static void EnsureErrorLogFileIsDeleted()
        {
            if (File.Exists(Memory.ErrorFilePath)) File.Delete(Memory.ErrorFilePath);
        }

        static void HandleException(Exception exception)
        {
            switch (exception)
            {
                case OperationCanceledException operationCanceledException:
                    if (operationCanceledException.CancellationToken.IsCancellationRequested) return;
                    break;
                case AggregateException aggregateException:
                    var thereIsNoUnhandledInnerException = !aggregateException.InnerExceptions.Any(innerException =>
                    {
                        if (!(innerException is OperationCanceledException operationCanceledException)) return true;
                        return !operationCanceledException.CancellationToken.IsCancellationRequested;
                    });
                    if (thereIsNoUnhandledInnerException) return;
                    break;
            }
            File.AppendAllText(Memory.ErrorFilePath, exception.ToString());
            OnExceptionOccurred?.Invoke(exception);
        }

        static void InitializeResourceCollectorPool()
        {
            var openedWebBrowserCount = 0;
            Parallel.For(0, Memory.Configurations.WebBrowserCount, resourceCollectorId =>
            {
                if (Memory.CancellationToken.IsCancellationRequested) throw new OperationCanceledException(Memory.CancellationToken);
                var resourceCollector = ServiceLocator.Get<IResourceCollector>();
                resourceCollector.OnRawResourceCollected += rawResource => Task.Run(() => { Memory.Memorize(rawResource); });
                resourceCollector.OnBrowserExceptionOccurred += (exception, resource) =>
                {
                    /* TODO: How and Where do we log this information? */
                };
                resourceCollector.OnAllAttemptsToCollectNewRawResourcesFailed += parentResource =>
                {
                    /* TODO: How and Where do we log this information? */
                };
                resourceCollector.OnIdle += () => ResourceCollectorPool.Add(resourceCollector);
                ResourceCollectorPool.Add(resourceCollector);
                OnWebBrowserOpened?.Invoke(Interlocked.Increment(ref openedWebBrowserCount));
            });
        }

        static void InitializeResourceVerifierPool()
        {
            for (var resourceVerifierId = 0; resourceVerifierId < 1000 * Memory.Configurations.WebBrowserCount; resourceVerifierId++)
            {
                if (Memory.CancellationToken.IsCancellationRequested) throw new OperationCanceledException(Memory.CancellationToken);
                var resourceVerifier = ServiceLocator.Get<IResourceVerifier>();
                resourceVerifier.OnIdle += () => ResourceVerifierPool.Add(resourceVerifier);
                ResourceVerifierPool.Add(resourceVerifier);
            }
        }

        public delegate void ExceptionOccurredEvent(Exception exception);
        public delegate Task ResourceVerifiedEvent(IVerificationResult verificationResult);
        public delegate void StoppedEvent(bool isAllWorkDone = false);
        public delegate void WebBrowserClosedEvent(int closedWebBrowserCount);
        public delegate void WebBrowserOpenedEvent(int openedWebBrowserCount);
    }
}