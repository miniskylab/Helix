using System;

namespace Helix.IPC.Abstractions
{
    public interface IIpcSocket : IDisposable
    {
        void On(string ipcTextMessage, Action<string> action);

        void Send(IIpcMessage ipcMessage);
    }
}