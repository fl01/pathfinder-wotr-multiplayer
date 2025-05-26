using BeetleX.Clients;

namespace WOTRMultiplayer.Networking
{
    public class NetworkServerClient
    {
        private AwaiterClient _client;

        public void Connect(string host, int port)
        {
            _client = new AwaiterClient(host, port, new Messages.ProtobufClientPacket());
        }
    }
}
