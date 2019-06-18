using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helix.IPC.Abstractions;
using Newtonsoft.Json;

namespace Helix.IPC
{
    public class SynchronousServerSocket : ISynchronousServerSocket
    {
        const char EndOfTransmissionCharacter = (char) 4;
        readonly CancellationTokenSource _cancellationTokenSource;
        Socket _incomingConnectionHandlerSocket;
        readonly Socket _incomingConnectionListenerSocket;
        readonly Task _incomingConnectionListeningTask;

        public event Action<Message> OnReceived;

        public SynchronousServerSocket(string ipAddressString, int port)
        {
            var ipAddress = IPAddress.Parse(ipAddressString);
            var ipEndPoint = new IPEndPoint(ipAddress, port);
            _incomingConnectionListenerSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _incomingConnectionListenerSocket.Bind(ipEndPoint);
            _incomingConnectionListenerSocket.Listen(1);

            var byteBuffer = new byte[1024];
            _cancellationTokenSource = new CancellationTokenSource();
            _incomingConnectionListeningTask = Task.Run(() =>
            {
                _incomingConnectionHandlerSocket = _incomingConnectionListenerSocket.Accept();
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Array.Clear(byteBuffer, 0, byteBuffer.Length);
                    var receivedByteCount = _incomingConnectionHandlerSocket.Receive(byteBuffer);
                    var receivedTextData = Encoding.ASCII.GetString(byteBuffer, 0, receivedByteCount);
                    if (string.IsNullOrEmpty(receivedTextData)) continue;

                    foreach (var receivedTextMessage in receivedTextData.Split(EndOfTransmissionCharacter))
                    {
                        if (string.IsNullOrWhiteSpace(receivedTextMessage)) continue;
                        var receivedMessage = JsonConvert.DeserializeObject<Message>(receivedTextMessage);
                        OnReceived?.Invoke(receivedMessage);
                    }
                }
                _incomingConnectionHandlerSocket.Shutdown(SocketShutdown.Both);
                _incomingConnectionHandlerSocket.Close();
            }, _cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            try
            {
                _incomingConnectionListenerSocket.Shutdown(SocketShutdown.Both);
                _incomingConnectionListenerSocket.Close();
            }
            catch (SocketException socketException)
            {
                if (socketException.SocketErrorCode != SocketError.NotConnected) throw;
            }

            try { Task.WhenAll(_incomingConnectionListeningTask).Wait(); }
            catch (SocketException socketException)
            {
                if (socketException.SocketErrorCode != SocketError.ConnectionAborted) throw;
            }

            _cancellationTokenSource.Dispose();
        }

        public void Send(Message message)
        {
            var byteStream = Encoding.ASCII.GetBytes($"{JsonConvert.SerializeObject(message)}{EndOfTransmissionCharacter}");
            _incomingConnectionHandlerSocket.Send(byteStream);
        }
    }
}