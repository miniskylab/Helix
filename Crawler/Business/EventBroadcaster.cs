using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class EventBroadcaster : IEventBroadcaster, IDisposable
    {
        CancellationTokenSource _cancellationTokenSource;
        BlockingCollection<Event> _events;
        bool _objectDisposed;

        public event Action<Event> OnEventBroadcast;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public EventBroadcaster()
        {
            _objectDisposed = false;
            _events = new BlockingCollection<Event>();
            _cancellationTokenSource = new CancellationTokenSource();

            Task.Run(() =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    var @event = _events.Take(_cancellationTokenSource.Token);
                    OnEventBroadcast?.Invoke(@event);
                }
            }, _cancellationTokenSource.Token);
        }

        public void Broadcast(Event @event)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(EventBroadcaster));
            _events.Add(@event);
        }

        public void Dispose()
        {
            if (_objectDisposed) return;
            _objectDisposed = true;

            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        void ReleaseUnmanagedResources()
        {
            _events?.Dispose();
            _events = null;

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        ~EventBroadcaster() { ReleaseUnmanagedResources(); }
    }
}