using System.Reflection;
using BeetleX;
using BeetleX.Buffers;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Networking
{
    public class TcpClientFactory : ITcpClientFactory
    {
        public ITcpClient Create(string host, int port)
        {
            var defaultGroup = typeof(BufferPoolGroup).GetField("mDefaultGroup", BindingFlags.NonPublic | BindingFlags.Static);

            BufferPool.POOL_SIZE = 4000;
            BufferPool.POOL_MAX_SIZE = 25000;
            BufferPool.BUFFER_SIZE = 1024 * 16;

            var client = SocketFactory.CreateClient<BeetleTcpClient>(new Messages.ProtobufClientPacket(), host, port);
            return client;
        }
    }
}
