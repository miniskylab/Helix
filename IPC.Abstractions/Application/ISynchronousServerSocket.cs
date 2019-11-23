using System;
using Helix.Core;

namespace Helix.IPC.Abstractions
{
    public interface ISynchronousServerSocket : IService, IDisposable
    {
        event Action<Message> OnReceived;

        void Send(Message message);
    }
}