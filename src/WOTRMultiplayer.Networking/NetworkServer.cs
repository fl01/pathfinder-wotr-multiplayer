using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BeetleX;
using BeetleX.EventArgs;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking
{
    public class NetworkServer : INetworkServer
    {
        private ServerBuilder<NetworkServerApp, NetworkClientToken, ProtobufPacket> _server;
        private ServerBuilder<NetworkServerApp, NetworkClientToken, ProtobufPacket> Server => _server ??= new ServerBuilder<NetworkServerApp, NetworkClientToken, ProtobufPacket>();

        public Action<long> OnClientConnected { get; set; }

        public Action<long> OnClientDisconnected { get; set; }

        public Action<EndPoint> OnServerStarted { get; set; }

        public bool IsActive => _server.AppServer?.Status == ServerStatus.Start;
        private Task _serverRunTask;
        private readonly ILogger<NetworkServer> _logger;

        public NetworkServer(ILogger<NetworkServer> logger)
        {
            _logger = logger;
        }

        public INetworkServer Register<TMessage>(Action<long, TMessage> messageHandler)
            where TMessage : class
        {
            Server.OnMessageReceive<TMessage>(args => messageHandler(args.NetSession.ID, args.Message));
            return this;
        }

        public void Start()
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
