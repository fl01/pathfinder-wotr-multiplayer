using BeetleX;
using BeetleX.Buffers;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Networking
{
    public class TcpClientFactory : ITcpClientFactory
    {
        public ITcpClient Create(string host, int port)
        {
            BufferPool.BUFFER_SIZE = NetworkingConsts.BufferSize;
            BufferPool.POOL_SIZE = 2048;
            BufferPool.POOL_MAX_SIZE = 61440;
            var client = SocketFactory.CreateClient<BeetleTcpClient>(new Messages.ProtobufClientPacket(), host, port);
            return client;
        }
    }
}
