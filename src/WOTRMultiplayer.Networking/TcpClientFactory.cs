using BeetleX;
using BeetleX.Buffers;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Networking
{
    public class TcpClientFactory : ITcpClientFactory
    {
        public ITcpClient Create(string host, int port)
        {
            BufferPool.POOL_MINI_SIZE = 6000;
            BufferPool.POOL_SIZE = 10000;
            BufferPool.POOL_MAX_SIZE = 80000;
            BufferPool.BUFFER_SIZE = 1024 * 32;

            var client = SocketFactory.CreateClient<BeetleTcpClient>(new Messages.ProtobufClientPacket(), host, port);
            return client;
        }
    }
}
