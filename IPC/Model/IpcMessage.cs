using Helix.IPC.Abstractions;

namespace Helix.IPC
{
    public class IpcMessage : IIpcMessage
    {
        public string Payload { get; set; }

        public string Text { get; set; }
    }
}