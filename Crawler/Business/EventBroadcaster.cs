using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class EventBroadcaster : IEventBroadcaster
    {
        CancellationTokenSource _cancellationTokenSource;
        readonly Task _eventBroadcastTask;
        BlockingCollection<Event> _events;
        bool _objectDisposed;

        public event Action<Event> OnEventBroadcast;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public EventBroadcaster()
        {
            _objectDisposed = false;
            _events = new BlockingCollection<Event>();
            _cancellationTokenSource = new CancellationTokenSource();

            _eventBroadcastTask = Task.Run(() =>
            {
                while (!CancellationRequestedAndNoEventInQueue())
                {
                    var @event = _events.Take(_cancellationTokenSource.Token);
                    if (@event == null) continue;

                    OnEventBroadcast?.Invoke(@event);
                }

                bool CancellationRequestedAndNoEventInQueue() => _cancellationTokenSource.IsCancellationRequested && _events.Count == 0;
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
            _cancellationTokenSource?.Cancel();
            _eventBroadcastTask?.Wait(); // TODO: TaskCancelledException
            _eventBroadcastTask?.Dispose();

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            if (_events == null) return;
            _events.Dispose();
            _events = null;
        }

        ~EventBroadcaster() { ReleaseUnmanagedResources(); }
    }
}