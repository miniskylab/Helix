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
        static readonly BlockingCollection<Resource> TobeVerifiedResources = new BlockingCollection<Resource>();
        static readonly Verifier Verifier = new Verifier();

        static void InitializeResourceCollectorPool()
        {
            for (var resourceCollectorId = 0; resourceCollectorId < Configurations.MaxCrawlerCount; resourceCollectorId++)
            {
                var resourceCollector = new ResourceCollector();
                resourceCollector.OnResourceCollected += resource =>
                {
                    lock (Lock)
                    {
                        if (AlreadyVerifiedUrls.TryGetValue(resource.Uri.AbsoluteUri, out _)) return;
                        AlreadyVerifiedUrls.TryAdd(resource.Uri.AbsoluteUri, true);
                    }
                    TobeVerifiedResources.Add(resource);
                };
                resourceCollector.OnIdle += () => ResourceCollectorPool.Add(resourceCollector);
                ResourceCollectorPool.Add(resourceCollector);
            }
        }

        static void Main()
        {
            InitializeResourceCollectorPool();
            TobeVerifiedResources.Add(new Resource(Configurations.StartUri));
            StartCrawl();
        }

        static void StartCrawl()
        {
            var crawlerPool = new Task[Configurations.MaxCrawlerCount];
            for (var crawlerId = 0; crawlerId < Configurations.MaxCrawlerCount; crawlerId++)
                crawlerPool[crawlerId] = Task.Factory.StartNew(() =>
                {
                    while (TobeVerifiedResources.Any() || _activeCrawlerCount > 0)
                    {
                        Thread.Sleep(100);
                        while (TobeVerifiedResources.TryTake(out var tobeVerifiedResource))
                        {
                            Interlocked.Increment(ref _activeCrawlerCount);
                            var verificationResult = Verifier.Verify(tobeVerifiedResource);
                            if (verificationResult.StatusCode >= 400 || !verificationResult.IsInternalUrl)
                            {
                                Interlocked.Decrement(ref _activeCrawlerCount);
                                continue;
                            }
                            ResourceCollectorPool.Take().CollectNewResourcesFrom(tobeVerifiedResource);
                            Interlocked.Decrement(ref _activeCrawlerCount);
                        }
                    }
                });
            Task.WhenAll(crawlerPool).Wait();

            Console.WriteLine("\nWork done! Cleaning Up ...");
            foreach (var resourceCollector in ResourceCollectorPool) resourceCollector.Dispose();
            Verifier.Dispose();

            Console.WriteLine("Shutting down in 15 seconds ...");
            Thread.Sleep(TimeSpan.FromSeconds(15));
            Console.ReadLine();
        }
    }
}