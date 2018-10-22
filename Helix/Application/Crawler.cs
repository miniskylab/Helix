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
        static readonly object Lock = new object();
        static readonly BlockingCollection<ResourceCollector> ResourceCollectorPool = new BlockingCollection<ResourceCollector>();
        static readonly ResourceVerifier ResourceVerifier = new ResourceVerifier();
        static readonly BlockingCollection<RawResource> TobeVerifiedRawResources = new BlockingCollection<RawResource>();

        static void InitializeResourceCollectorPool()
        {
            for (var resourceCollectorId = 0; resourceCollectorId < Configurations.MaxCrawlerCount; resourceCollectorId++)
            {
                var resourceCollector = new ResourceCollector();
                resourceCollector.OnRawResourceCollected += rawResource =>
                {
                    lock (Lock)
                    {
                        if (AlreadyVerifiedUrls.TryGetValue(rawResource.Url, out _)) return;
                        AlreadyVerifiedUrls.TryAdd(rawResource.Url, true);
                    }
                    TobeVerifiedRawResources.Add(rawResource);
                };
                resourceCollector.OnIdle += () => ResourceCollectorPool.Add(resourceCollector);
                ResourceCollectorPool.Add(resourceCollector);
            }
        }

        static void Main()
        {
            TobeVerifiedRawResources.Add(new RawResource { Url = Configurations.StartUrl, ParentUrl = null });
            InitializeResourceCollectorPool();
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
                            var verificationResult = ResourceVerifier.Verify(tobeVerifiedRawResource);
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
            ResourceVerifier.Dispose();

            Console.WriteLine("Shutting down in 15 seconds ...");
            Thread.Sleep(TimeSpan.FromSeconds(15));
            Console.ReadLine();
        }
    }
}