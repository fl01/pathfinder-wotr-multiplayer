using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BeetleX;
using BeetleX.EventArgs;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking
{
    public class NetworkServer : IApplication, IDisposable
    {
        private ServerBuilder<NetworkServer, NetworkClientToken, ProtobufPacket> _server;
        private ServerBuilder<NetworkServer, NetworkClientToken, ProtobufPacket> Server => _server ??= new ServerBuilder<NetworkServer, NetworkClientToken, ProtobufPacket>();

        public Action<long> OnClientConnected { get; set; }

        public Action<long> OnClientDisconnected { get; set; }

        public Action<EndPoint> OnServerStarted { get; set; }

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

        public void Send(long clientId, object message)
        {
            var session = Server.AppServer.GetSession(clientId);
            if (session == null)
            {
                return;
            }

            Server.AppServer.Send(message, session);
        }

        public void SendAll(object message)
        {
            Server.AppServer.Send(message, Server.AppServer.GetOnlines());
        }

        public void SendAllExcept(long clientId, object message)
        {
            var sessions = Server.AppServer.GetOnlines().Where(s => s.ID != clientId).ToArray();
            Server.AppServer.Send(message, sessions);
        }

        private void OnOpened(IServer server)
        {
            var endpoint = server.Options?.DefaultListen?.EndPoint;
            if (endpoint == null)
            {
                // error
                return;
            }

            OnServerStarted?.Invoke(endpoint);
        }

        public void Init(IServer server)
        {
        }

        private void OnServerLog(IServer server, ServerLogEventArgs args)
        {
        }

        private void OnDisconnected(ISession session, NetworkClientToken clientToken)
        {
            OnClientDisconnected?.Invoke(session.ID);
        }

        private void OnConnected(ISession session, NetworkClientToken clientToken)
        {
            OnClientConnected?.Invoke(session.ID);
        }

        public void Dispose()
        {
            _server.Dispose();
        }
    }
}
