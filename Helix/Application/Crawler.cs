using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Helix
{
    static class Crawler
    {
        static int _activeCrawlerCount;
        static readonly ConcurrentDictionary<string, bool> AlreadyVerifiedUrls = new ConcurrentDictionary<string, bool>();
        static readonly BlockingCollection<ResourceCollector> ResourceCollectorPool = new BlockingCollection<ResourceCollector>();
        static readonly BlockingCollection<ResourceVerifier> ResourceVerifierPool = new BlockingCollection<ResourceVerifier>();
        static readonly object StaticLock = new object();
        static readonly BlockingCollection<RawResource> TobeVerifiedRawResources = new BlockingCollection<RawResource>();

        static void InitializeResourceCollectorPool()
        {
            for (var resourceCollectorId = 0; resourceCollectorId < Configurations.MaxCrawlerCount; resourceCollectorId++)
            {
                var resourceCollector = new ResourceCollector();
                resourceCollector.OnRawResourceCollected += rawResource =>
                {
                    lock (StaticLock)
                    {
                        if (AlreadyVerifiedUrls.TryGetValue(rawResource.Url.StripFragment(), out _)) return;
                        AlreadyVerifiedUrls.TryAdd(rawResource.Url.StripFragment(), true);
                    }
                    TobeVerifiedRawResources.Add(rawResource);
                };
                resourceCollector.OnExceptionOccurred += (exception, resource) =>
                {
                    /* TODO: How and Where do we log this information? */
                };
                resourceCollector.OnAllAttemptsToCollectResourcesFailed += resource =>
                {
                    /* TODO: How and Where do we log this information? */
                };
                resourceCollector.OnIdle += () => ResourceCollectorPool.Add(resourceCollector);
                ResourceCollectorPool.Add(resourceCollector);
            }
        }

        static void InitializeResourceVerifierPool()
        {
            for (var resourceVerifierId = 0; resourceVerifierId < Configurations.MaxCrawlerCount; resourceVerifierId++)
            {
                var resourceVerifier = new ResourceVerifier();
                resourceVerifier.OnIdle += () => ResourceVerifierPool.Add(resourceVerifier);
                ResourceVerifierPool.Add(resourceVerifier);
            }
        }

        static void Main()
        {
            lock (StaticLock) { AlreadyVerifiedUrls.TryAdd(Configurations.StartUrl, true); }
            TobeVerifiedRawResources.Add(new RawResource { Url = Configurations.StartUrl, ParentUrl = null });
            InitializeResourceCollectorPool();
            InitializeResourceVerifierPool();
            StartCrawl();
        }

        static void StartCrawl()
        {
            var crawlerPool = new Task[Configurations.MaxCrawlerCount];
            for (var crawlerId = 0; crawlerId < Configurations.MaxCrawlerCount; crawlerId++)
                crawlerPool[crawlerId] = Task.Factory.StartNew(() =>
                {
                    while (TobeVerifiedRawResources.Any() || _activeCrawlerCount > 0)
                    {
                        Thread.Sleep(100);
                        while (TobeVerifiedRawResources.TryTake(out var tobeVerifiedRawResource))
                        {
                            Interlocked.Increment(ref _activeCrawlerCount);
                            var verificationResult = ResourceVerifierPool.Take().Verify(tobeVerifiedRawResource);
                            if (verificationResult.IsBrokenResource || !verificationResult.IsInternalResource)
                            {
                                Interlocked.Decrement(ref _activeCrawlerCount);
                                continue;
                            }
                            ResourceCollectorPool.Take().CollectNewRawResourcesFrom(verificationResult.Resource);
                            Interlocked.Decrement(ref _activeCrawlerCount);
                        }
                    }
                });
            Task.WhenAll(crawlerPool).Wait();

            Console.WriteLine("\nWork done! Cleaning Up ...");
            foreach (var resourceCollector in ResourceCollectorPool) resourceCollector.Dispose();
            foreach (var resourceVerifier in ResourceVerifierPool) resourceVerifier.Dispose();

            Console.WriteLine("Press any key to quit ...");
            Console.ReadLine();
        }
    }
}