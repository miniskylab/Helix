using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        readonly ConcurrentSet<Task> _backgroundTasks = new ConcurrentSet<Task>();
        readonly BlockingCollection<IResource> _toBeCrawledResources = new BlockingCollection<IResource>();
        readonly BlockingCollection<IRawResource> _toBeVerifiedRawResources = new BlockingCollection<IRawResource>();
        readonly string _workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        static readonly object StaticLock = new object();

        public CancellationTokenSource CancellationTokenSource { get; }

        public Configurations Configurations { get; }

        public CrawlerState CrawlerState { get; private set; } = CrawlerState.Ready;

        public string ErrorFilePath { get; }

        public bool AllBackgroundTasksAreDone => !_backgroundTasks.Any();

        public IEnumerable<Task> BackgroundTasks => _backgroundTasks;

        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        public bool EverythingIsDone => !_toBeVerifiedRawResources.Any() && !_toBeCrawledResources.Any() && AllBackgroundTasksAreDone;

        public int RemainingUrlCount => _backgroundTasks.Count + _toBeVerifiedRawResources.Count;

        public Memory(Configurations configurations)
        {
            Configurations = configurations;
            ErrorFilePath = Path.Combine(_workingDirectory, "errors.txt");
            CancellationTokenSource = new CancellationTokenSource();
            _backgroundTasks.Clear();
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

        public void Forget(Task backgroundTask)
        {
            if (CancellationToken.IsCancellationRequested) return;
            _backgroundTasks.Remove(backgroundTask);
        }

        public void ForgetAllBackgroundTasks() { _backgroundTasks.Clear(); }

        public void Memorize(IRawResource toBeVerifiedRawResource)
        {
            if (CancellationToken.IsCancellationRequested) return;
            lock (StaticLock)
            {
                if (_alreadyVerifiedUrls.Contains(toBeVerifiedRawResource.Url.StripFragment())) return;
                _alreadyVerifiedUrls.Add(toBeVerifiedRawResource.Url.StripFragment());
            }
            _toBeVerifiedRawResources.Add(toBeVerifiedRawResource, CancellationToken);
        }

        public void Memorize(IResource toBeCrawledResource)
        {
            if (CancellationToken.IsCancellationRequested) return;
            _toBeCrawledResources.Add(toBeCrawledResource, CancellationToken);
        }

        public void Memorize(Task backgroundTask) { _backgroundTasks.Add(backgroundTask); }

        public bool TryTakeToBeCrawledResource(out IResource toBeCrawledResource)
        {
            return _toBeCrawledResources.TryTake(out toBeCrawledResource);
        }

        public bool TryTakeToBeVerifiedRawResource(out IRawResource toBeVerifiedRawResource)
        {
            return _toBeVerifiedRawResources.TryTake(out toBeVerifiedRawResource);
        }

        public bool TryTransitTo(CrawlerState crawlerState)
        {
            if (CrawlerState == CrawlerState.Unknown) return false;
            switch (crawlerState)
            {
                case CrawlerState.Ready:
                    lock (StaticLock)
                    {
                        if (CrawlerState != CrawlerState.Stopping) return false;
                        CrawlerState = CrawlerState.Ready;
                        return true;
                    }
                case CrawlerState.Working:
                    lock (StaticLock)
                    {
                        if (CrawlerState != CrawlerState.Ready && CrawlerState != CrawlerState.Paused) return false;
                        CrawlerState = CrawlerState.Working;
                        return true;
                    }
                case CrawlerState.Stopping:
                    lock (StaticLock)
                    {
                        if (CrawlerState != CrawlerState.Working && CrawlerState != CrawlerState.Paused) return false;
                        CrawlerState = CrawlerState.Stopping;
                        return true;
                    }
                case CrawlerState.Paused:
                    lock (StaticLock)
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

        ~Memory()
        {
            CancellationTokenSource?.Dispose();
            _toBeVerifiedRawResources?.Dispose();
        }
    }
}