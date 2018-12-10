using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helix.IPC.Abstractions;
using Newtonsoft.Json;

namespace Helix.IPC
{
    public class IpcSocket : IIpcSocket
    {
        readonly Dictionary<string, Action<string>> _actions;
        readonly List<Task> _backgroundTasks;
        readonly CancellationTokenSource _cancellationTokenSource;
        Socket _handlerSocket;
        readonly Socket _listenerSocket;

        public IpcSocket(string ipAddressString, int port)
        {
            var ipAddress = IPAddress.Parse(ipAddressString);
            var ipEndPoint = new IPEndPoint(ipAddress, port);
            _listenerSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listenerSocket.Bind(ipEndPoint);
            _listenerSocket.Listen(1);

            var byteBuffer = new byte[1024];
            _actions = new Dictionary<string, Action<string>>();
            _cancellationTokenSource = new CancellationTokenSource();
            _backgroundTasks = new List<Task>
            {
                Task.Run(() =>
                {
                    _handlerSocket = _listenerSocket.Accept();
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        Array.Clear(byteBuffer, 0, byteBuffer.Length);
                        var receivedByteCount = _handlerSocket.Receive(byteBuffer);
                        var ipcRawMessageFromRemote = Encoding.ASCII.GetString(byteBuffer, 0, receivedByteCount);
                        var ipcMessageFromRemote = JsonConvert.DeserializeObject<IpcMessage>(ipcRawMessageFromRemote);
                        if (_actions.TryGetValue(ipcMessageFromRemote.Text, out var action)) action.Invoke(ipcMessageFromRemote.Payload);
                    }
                }, _cancellationTokenSource.Token)
            };
        }

        public void Dispose()
        {
            _listenerSocket.Shutdown(SocketShutdown.Both);
            _listenerSocket.Close();

            _handlerSocket?.Shutdown(SocketShutdown.Both);
            _handlerSocket?.Close();

            _cancellationTokenSource.Cancel();
            Task.WhenAll(_backgroundTasks).Wait();
            _cancellationTokenSource.Dispose();
        }

        public void On(string ipcTextMessage, Action<string> action) { _actions[ipcTextMessage] = action; }

        public void Send(IIpcMessage ipcMessage) { _handlerSocket.Send(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(ipcMessage))); }
    }
}