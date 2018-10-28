using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler
{
    public static class Crawler
    {
        static int _activeThreadCount;
        static readonly ConcurrentDictionary<string, bool> AlreadyVerifiedUrls = new ConcurrentDictionary<string, bool>();
        static readonly BlockingCollection<ResourceCollector> ResourceCollectorPool = new BlockingCollection<ResourceCollector>();
        static readonly BlockingCollection<ResourceVerifier> ResourceVerifierPool = new BlockingCollection<ResourceVerifier>();
        static readonly object StaticLock = new object();
        static readonly BlockingCollection<RawResource> TobeVerifiedRawResources = new BlockingCollection<RawResource>();

        public static void StartWorking()
        {
            var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var errorFilePath = Path.Combine(workingDirectory, "error.txt");

            try
            {
                if (File.Exists(errorFilePath)) File.Delete(errorFilePath);
                lock (StaticLock) { AlreadyVerifiedUrls.TryAdd(Configurations.StartUrl, true); }
                TobeVerifiedRawResources.Add(new RawResource { Url = Configurations.StartUrl, ParentUrl = null });
                InitializeResourceCollectorPool();
                InitializeResourceVerifierPool();
                DoWorkInParallel();
            }
            catch (Exception exception)
            {
                File.WriteAllText(errorFilePath, exception.ToString());
                Console.WriteLine(exception.ToString());
            }
            finally
            {
                Console.WriteLine("\nCleaning Up ...");
                foreach (var resourceCollector in ResourceCollectorPool) resourceCollector.Dispose();
                foreach (var resourceVerifier in ResourceVerifierPool) resourceVerifier.Dispose();

                Console.WriteLine("Press any key to quit ...");
                Console.ReadLine();
            }
        }

        static void Crawl()
        {
            while (TobeVerifiedRawResources.Any() || _activeThreadCount > 0)
            {
                Thread.Sleep(100);
                while (TobeVerifiedRawResources.TryTake(out var tobeVerifiedRawResource))
                {
                    Interlocked.Increment(ref _activeThreadCount);
                    var verificationResult = ResourceVerifierPool.Take().Verify(tobeVerifiedRawResource);
                    if (verificationResult.IsBrokenResource || !verificationResult.IsInternalResource)
                    {
                        Interlocked.Decrement(ref _activeThreadCount);
                        continue;
                    }
                    ResourceCollectorPool.Take().CollectNewRawResourcesFrom(verificationResult.Resource);
                    Interlocked.Decrement(ref _activeThreadCount);
                }
            }
        }

        static void DoWorkInParallel()
        {
            var tasks = new Task[Configurations.MaxThreadCount];
            for (var taskId = 0; taskId < Configurations.MaxThreadCount; taskId++)
                tasks[taskId] = Task.Factory.StartNew(Crawl);
            Task.WhenAll(tasks).Wait();
        }

        static void InitializeResourceCollectorPool()
        {
            for (var resourceCollectorId = 0; resourceCollectorId < Configurations.MaxThreadCount; resourceCollectorId++)
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
                resourceCollector.OnAllAttemptsToCollectNewRawResourcesFailed += parentResource =>
                {
                    /* TODO: How and Where do we log this information? */
                };
                resourceCollector.OnIdle += () => ResourceCollectorPool.Add(resourceCollector);
                ResourceCollectorPool.Add(resourceCollector);
            }
        }

        static void InitializeResourceVerifierPool()
        {
            for (var resourceVerifierId = 0; resourceVerifierId < Configurations.MaxThreadCount; resourceVerifierId++)
            {
                var resourceVerifier = new ResourceVerifier();
                resourceVerifier.OnIdle += () => ResourceVerifierPool.Add(resourceVerifier);
                ResourceVerifierPool.Add(resourceVerifier);
            }
        }
    }
}