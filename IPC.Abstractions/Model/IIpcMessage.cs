namespace Helix.IPC.Abstractions
{
    public interface IIpcMessage
    {
        string Payload { get; set; }

        string Text { get; set; }
    }
}