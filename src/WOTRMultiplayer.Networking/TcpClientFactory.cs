using BeetleX;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Networking
{
    public class TcpClientFactory : ITcpClientFactory
    {
        public ITcpClient Create(string host, int port)
        {
            var client = SocketFactory.CreateClient<BeetleTcpClient>(new Messages.ProtobufClientPacket(), host, port);
            return client;
        }
    }
}
