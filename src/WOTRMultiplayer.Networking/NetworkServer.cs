using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using BeetleX;
using BeetleX.EventArgs;
using WOTRMultiplayer.Networking.Messages;
using WOTRMultiplayer.Networking.Messages.System;

namespace WOTRMultiplayer.Networking
{
    public class NetworkServer : IApplication, IDisposable
    {
        private ServerBuilder<NetworkServer, NetworkClientToken, ProtobufPacket> _server;
        private ServerBuilder<NetworkServer, NetworkClientToken, ProtobufPacket> Server => _server ??= new ServerBuilder<NetworkServer, NetworkClientToken, ProtobufPacket>();

        private readonly ConcurrentDictionary<long, NetworkClient> _clients = new();

        public bool IsActive => _server.AppServer?.Status == ServerStatus.Start;
        private Task _serverRunTask;

        public NetworkServer Register<TMessage>(Action<long, TMessage> handler)
        {
            Server.OnMessageReceive<TMessage>(args => handler(args.NetSession.ID, args.Message));
            return this;
        }

        public void Start(string networkInterfaceBinding, int port)
        {
            Server.ServerOptions.DefaultListen.StartRegionPort = 1024;
            Server.ServerOptions.DefaultListen.EndRegionPort = ushort.MaxValue;
            Server.OnOpened(OnOpened);
            Server.OnLog(OnServerLog)
                .OnConnected(OnConnected)
                .OnDisconnect(OnDisconnected);

            _serverRunTask = Server.Run();
        }

        private void OnOpened(IServer server)
        {
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

        public void Dispose()
        {
            _server.Dispose();
        }
    }
}
