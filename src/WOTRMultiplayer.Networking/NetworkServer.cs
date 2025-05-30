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
        private Task _serverRunTask;
        private readonly ILogger<NetworkServer> _logger;

        public Action<long> OnClientConnected { get; set; }
        public Action<long> OnClientDisconnected { get; set; }
        public Action<EndPoint> OnServerStarted { get; set; }
        public bool IsActive => Server.AppServer?.Status == ServerStatus.Start;

        public NetworkServer(ILogger<NetworkServer> logger)
        {
            _logger = logger;
        }

        public INetworkServer Register<TMessage>(Action<long, TMessage> messageHandler)
            where TMessage : class
        {
            _logger.LogInformation("Register message handler. Type={type}", typeof(TMessage).Name);
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
                _logger.LogWarning("Client doesn't exist. ClientId={clientId}", clientId);
                return;
            }

            Server.AppServer.Send(message, session);
        }

        public void SendAll(object message)
        {
            var sessions = Server.AppServer.GetOnlines();
            Server.AppServer.Send(message, sessions);
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
                _logger.LogError("Server started with null endpoint");
                return;
            }

            _logger.LogInformation("Server started. Endpoint={endpoint}", endpoint);

            try
            {
                OnServerStarted?.Invoke(endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle OnServerStarted");
            }
        }

        private void OnServerLog(IServer server, ServerLogEventArgs args)
        {
        }

        private void OnDisconnected(ISession session, NetworkClientToken clientToken)
        {
            _logger.LogInformation("Client disconnected. ClientId={clientId}", session.ID);
            try
            {
                OnClientDisconnected?.Invoke(session.ID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle OnDisconnected");
            }
        }

        private void OnConnected(ISession session, NetworkClientToken clientToken)
        {
            _logger.LogInformation("Client connected. ClientId={clientId}", session.ID);
            try
            {
                OnClientConnected?.Invoke(session.ID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle OnClientConnected");
            }
        }

        public void Dispose()
        {
            _logger.LogInformation("Dispose");
            _server?.Dispose();
        }
    }
}
