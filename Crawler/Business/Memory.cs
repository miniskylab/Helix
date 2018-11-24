using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Helix.Abstractions;
using JetBrains.Annotations;

namespace Helix.Implementations
{
    class Memory : IMemory
    {
        readonly ConcurrentSet<string> _alreadyVerifiedUrls = new ConcurrentSet<string>();
        readonly ConcurrentSet<Task> _backgroundCrawlingTasks = new ConcurrentSet<Task>();
        readonly BlockingCollection<IRawResource> _toBeVerifiedRawResources = new BlockingCollection<IRawResource>();
        readonly string _workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        static readonly object StaticLock = new object();

        public CancellationTokenSource CancellationTokenSource { get; }

        public Configurations Configurations { get; }

        public CrawlerState CrawlerState { get; private set; } = CrawlerState.Ready;

        public string ErrorFilePath { get; }

        public Task AllBackgroundCrawlingTasks => Task.WhenAll(_backgroundCrawlingTasks);

        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        public bool IsAllWorkDone => !_toBeVerifiedRawResources.Any() && Task.WhenAll(_backgroundCrawlingTasks).IsCompletedSuccessfully;

        public int RemainingUrlCount => _backgroundCrawlingTasks.Count + _toBeVerifiedRawResources.Count;

        public Memory(Configurations configurations)
        {
            Configurations = configurations;
            ErrorFilePath = Path.Combine(_workingDirectory, "errors.txt");
            CancellationTokenSource = new CancellationTokenSource();
            _backgroundCrawlingTasks.Clear();
            while (_toBeVerifiedRawResources.Any()) _toBeVerifiedRawResources.Take();
            lock (StaticLock)
            {
                _alreadyVerifiedUrls.Clear();
                _alreadyVerifiedUrls.Add(Configurations.StartUrl);
                _toBeVerifiedRawResources.Add(new RawResource { Url = Configurations.StartUrl, ParentUrl = null }, CancellationToken);
            }
        }

        [UsedImplicitly]
        public Memory() { }

        public void Forget(Task backgroundCrawlingTask)
        {
            if (CancellationToken.IsCancellationRequested) return;
            _backgroundCrawlingTasks.Remove(backgroundCrawlingTask);
        }

        public void ForgetAllBackgroundCrawlingTasks() { _backgroundCrawlingTasks.Clear(); }

        public void Memorize(IRawResource rawResource)
        {
            if (CancellationToken.IsCancellationRequested) return;
            lock (StaticLock)
            {
                if (_alreadyVerifiedUrls.Contains(rawResource.Url.StripFragment())) return;
                _alreadyVerifiedUrls.Add(rawResource.Url.StripFragment());
            }
            _toBeVerifiedRawResources.Add(rawResource, CancellationToken);
        }

        public void Memorize(Task backgroundCrawlingTask) { _backgroundCrawlingTasks.Add(backgroundCrawlingTask); }

        public bool TryTakeToBeVerifiedRawResource(out IRawResource rawResource)
        {
            return _toBeVerifiedRawResources.TryTake(out rawResource);
        }

        public bool TryTransitTo(CrawlerState crawlerState)
        {
            switch (crawlerState)
            {
                case CrawlerState.Ready:
                    lock (StaticLock)
                    {
                        if (CrawlerState == CrawlerState.Unknown || CrawlerState == CrawlerState.Ready) break;
                        CrawlerState = CrawlerState.Ready;
                        return true;
                    }
                case CrawlerState.Working:
                    lock (StaticLock)
                    {
                        if (CrawlerState == CrawlerState.Unknown || CrawlerState != CrawlerState.Ready) break;
                        CrawlerState = CrawlerState.Working;
                        return true;
                    }
                case CrawlerState.Paused:
                    throw new NotSupportedException();
                case CrawlerState.Unknown:
                    throw new NotSupportedException();
                default:
                    return false;
            }
            return false;
        }

        ~Memory()
        {
            CancellationTokenSource?.Dispose();
            _toBeVerifiedRawResources?.Dispose();
        }
    }
}