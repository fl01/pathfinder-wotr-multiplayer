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

            BufferPool.BUFFER_SIZE = 1024 * 64;
            var numberOfGroups = 12;
            var count = BufferPool.POOL_SIZE * 8 / numberOfGroups;
            var maxCount = BufferPool.POOL_MAX_SIZE * 8 / numberOfGroups;
            var defaultGroupValue = new BufferPoolGroup(BufferPool.BUFFER_SIZE, count, maxCount, numberOfGroups);
            defaultGroup.SetValue(null, defaultGroupValue);

            var client = SocketFactory.CreateClient<BeetleTcpClient>(new Messages.ProtobufClientPacket(), host, port);
            return client;
        }
    }
}
