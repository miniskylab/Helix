using System;

namespace Helix.IPC.Abstractions
{
    public interface ISynchronousServerSocket : IDisposable
    {
        event Action<Message> OnReceived;

        void Send(Message message);
    }
}