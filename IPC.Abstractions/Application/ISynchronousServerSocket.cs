using System;

namespace Helix.IPC.Abstractions
{
    public interface ISynchronousServerSocket : IDisposable
    {
        void On(string textMessage, Action<string> action);

        void Send(Message message);
    }
}