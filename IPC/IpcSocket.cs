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
        const char EndOfTransmissionCharacter = (char) 4;
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
                        var stringMessageFromClient = Encoding.ASCII.GetString(byteBuffer, 0, receivedByteCount);
                        if (string.IsNullOrEmpty(stringMessageFromClient)) continue;

                        var messageFromClient = JsonConvert.DeserializeObject<IpcMessage>(stringMessageFromClient);
                        if (_actions.TryGetValue(messageFromClient.Text, out var action)) action(messageFromClient.Payload);
                    }
                }, _cancellationTokenSource.Token)
            };
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            try { _listenerSocket.Shutdown(SocketShutdown.Both); }
            catch (SocketException socketException)
            {
                if (socketException.SocketErrorCode != SocketError.NotConnected) throw;
            }

            _handlerSocket?.Shutdown(SocketShutdown.Both);
            _listenerSocket.Close();
            _handlerSocket?.Close();

            try { Task.WhenAll(_backgroundTasks).Wait(); }
            catch (SocketException socketException)
            {
                if (socketException.SocketErrorCode != SocketError.ConnectionAborted) throw;
            }

            _cancellationTokenSource.Dispose();
        }

        public void On(string ipcTextMessage, Action<string> action) { _actions[ipcTextMessage] = action; }

        public void Send(IpcMessage ipcMessage)
        {
            var byteStream = Encoding.ASCII.GetBytes($"{JsonConvert.SerializeObject(ipcMessage)}{EndOfTransmissionCharacter}");
            _handlerSocket.Send(byteStream);
        }
    }
}