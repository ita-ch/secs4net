using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Secs4Net
{
    internal sealed class NetworkPipeline : IDecodeReader//, IEncodeWriter
    {
        private readonly Socket _socket;
        public NetworkPipeline(Socket socket)
        {
            _socket = socket;
        }

        //ValueTask<int> IEncodeWriter.WriteAsync(Memory<byte> buffer)
        //    => _socket.SendAsync(buffer, SocketFlags.None);

        ValueTask<int> IDecodeReader.ReadAsync(Memory<byte> buffer)
            => _socket.ReceiveAsync(buffer, SocketFlags.None);
    }
}
