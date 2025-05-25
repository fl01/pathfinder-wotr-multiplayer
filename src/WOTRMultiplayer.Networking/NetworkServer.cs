using System.Collections.Concurrent;
using BeetleX;
using BeetleX.EventArgs;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking
{
    public class NetworkServer : IApplication
    {
        private ServerBuilder<NetworkServer, NetworkClientToken, ProtobufPacket> _server;
        private ConcurrentDictionary<long, NetworkClient> _clients = new();

        public void Start()
        {
            _server = new ServerBuilder<NetworkServer, NetworkClientToken, ProtobufPacket>();
            _server.OnLog(OnServerLog)
                .OnConnected(OnConnected)
                .OnDisconnect(OnDisconnected);

            _server.Run();
        }

        public void Stop()
        {
            _server.Dispose();
        }

        public void Init(IServer server)
        {
            // get listen info -> show info label somewhere
        }

        private void OnServerLog(IServer server, ServerLogEventArgs args)
        {
        }

        private void OnDisconnected(ISession session, NetworkClientToken clientToken)
        {
            _clients.TryRemove(session.ID, out var _);
        }

        private void OnConnected(ISession session, NetworkClientToken clientToken)
        {
            var networkClient = new NetworkClient(session.ID);
            _clients.TryAdd(networkClient.Id, networkClient);
        }
    }
}
