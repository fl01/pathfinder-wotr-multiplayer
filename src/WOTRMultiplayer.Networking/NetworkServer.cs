using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using BeetleX;
using BeetleX.EventArgs;
using WOTRMultiplayer.Networking.Messages;
using WOTRMultiplayer.Networking.Messages.System;

namespace WOTRMultiplayer.Networking
{
    public class NetworkServer : IApplication
    {
        private ServerBuilder<NetworkServer, NetworkClientToken, ProtobufPacket> _server;
        private readonly ConcurrentDictionary<long, NetworkClient> _clients = new();

        public bool IsActive => _server.AppServer?.Status == ServerStatus.Start;
        private Task _serverRunTask;

        public NetworkServer()
        {
            _server = new ServerBuilder<NetworkServer, NetworkClientToken, ProtobufPacket>();
        }

        public void Register<T>(Action<T> handler)
        {
            _server.OnMessageReceive<T>(args => handler(args.Message));
        }

        public void Start(string networkInterfaceBinding, int port)
        {
            _server.ServerOptions.DefaultListen.StartRegionPort = 1024;
            _server.ServerOptions.DefaultListen.EndRegionPort = ushort.MaxValue;
            _server.OnOpened(OnOpened);
            _server.OnLog(OnServerLog)
                .OnConnected(OnConnected)
                .OnDisconnect(OnDisconnected);

            _serverRunTask = _server.Run();
        }

        private void OnOpened(IServer server)
        {
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

            session.Send(new NetworkClientNameRequest());
        }
    }
}
