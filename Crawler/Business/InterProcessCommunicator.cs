using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Helix.Implementations
{
    public class InterProcessCommunicator : IDisposable
    {
        const int Port = 18880;
        readonly Socket _handlerSocket;
        static InterProcessCommunicator _instance;

        public static InterProcessCommunicator Instance => _instance ?? (_instance = new InterProcessCommunicator());

        InterProcessCommunicator()
        {
            var ipAddress = IPAddress.Parse("127.0.0.1");
            var ipEndPoint = new IPEndPoint(ipAddress, Port);
            var listenerSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenerSocket.Bind(ipEndPoint);
            listenerSocket.Listen(1);
            _handlerSocket = listenerSocket.Accept();
        }

        public void Dispose()
        {
            _handlerSocket?.Shutdown(SocketShutdown.Both);
            _handlerSocket?.Close();
            _handlerSocket?.Dispose();
            _instance = null;
        }

        public void On(string message, Action callback)
        {
            var byteBuffer = new byte[1024];
            var receivedByteCount = _handlerSocket.Receive(byteBuffer);
            var messageFromRemote = Encoding.ASCII.GetString(byteBuffer, 0, receivedByteCount);
            if (messageFromRemote.Equals(message)) callback();
        }

        public void Send(string message) { _handlerSocket.Send(Encoding.ASCII.GetBytes(message)); }
    }
}