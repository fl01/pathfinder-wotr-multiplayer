using BeetleX;
using BeetleX.Buffers;
using WOTRMultiplayer.Networking.Abstractions.TCP;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking.Channels.TCP
{
    public class TcpFactory : ITcpFactory
    {
        public ITcpClient CreateClient(string host, int port)
        {
            BufferPool.POOL_MINI_SIZE = 6000;
            BufferPool.POOL_SIZE = 10000;
            BufferPool.POOL_MAX_SIZE = 80000;
            BufferPool.BUFFER_SIZE = 1024 * 32;

            var client = SocketFactory.CreateClient<BeetleTcpClient>(new BeetleXMessageTypes.ProtobufClientPacket(), host, port);
            return client;
        }

        public ServerBuilder<TApp, TToken, TPacket> CreateServerBuilder<TApp, TToken, TPacket>()
            where TApp : IApplication, new()
            where TToken : ISessionToken, new()
            where TPacket : IPacket, new()
        {
            return new();
        }
    }
}
