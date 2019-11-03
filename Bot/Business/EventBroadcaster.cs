using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Helix.Bot.Abstractions;

namespace Helix.Bot
{
    public sealed class EventBroadcaster : IEventBroadcaster
    {
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly Task _eventBroadcastTask;
        readonly BlockingCollection<Event> _events;
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
                try
                {
                    while (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        var @event = _events.Take(_cancellationTokenSource.Token);
                        OnEventBroadcast?.Invoke(@event);
                    }
                }
                catch (Exception exception) when (exception.IsAcknowledgingOperationCancelledException(_cancellationTokenSource.Token))
                {
                    while (_events.TryTake(out var @event))
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

            _cancellationTokenSource?.Cancel();
            _eventBroadcastTask?.Wait();
            _eventBroadcastTask?.Dispose();
            _cancellationTokenSource?.Dispose();
            _events?.Dispose();

            _objectDisposed = true;
        }
    }
}