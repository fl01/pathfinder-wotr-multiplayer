using System;
using System.Linq;
using System.Net;
using BeetleX;
using BeetleX.EventArgs;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Logging.Extensions;
using WOTRMultiplayer.Networking.Abstractions.TCP;
using WOTRMultiplayer.Networking.Configuration;
using WOTRMultiplayer.Networking.Consuming;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking.Channels.TCP
{
    public class NetworkServer : INetworkServer
    {
        private ServerBuilder<TcpServerApp, TcpSessionToken, BeetleXMessageTypes.ProtobufServerPacket> _server;
        private readonly ILogger<NetworkServer> _logger;
        private readonly ITcpFactory _tcpFactory;

        public Action<long> OnClientConnected { get; set; }

        public Action<long> OnClientDisconnected { get; set; }

        public Action<EndPoint> OnServerStarted { get; set; }

        public bool IsActive => _server?.AppServer?.Status == ServerStatus.Start;

        public Action<NetworkMessageMetadata> OnMessageReceived { get; set; }

        public NetworkServer(
            ILogger<NetworkServer> logger,
            ITcpFactory tcpFactory)
        {
            _logger = logger;
            _tcpFactory = tcpFactory;
        }

        public void Start(NetworkServerConfiguration networkServerConfiguration)
        {
            if (_server != null)
            {
                Reset();
            }

            _server = _tcpFactory.CreateServerBuilder<TcpServerApp, TcpSessionToken, BeetleXMessageTypes.ProtobufServerPacket>();
            _server.ServerOptions.DefaultListen.Host = networkServerConfiguration.Host;
            _server.ServerOptions.UseIPv6 = networkServerConfiguration.UseIPv6;
            _server.ServerOptions.DefaultListen.StartRegionPort = networkServerConfiguration.PortRangeStart;
            _server.ServerOptions.DefaultListen.EndRegionPort = networkServerConfiguration.PortRangeEnd;
            _server.ServerOptions.BufferSize = 1024 * 32;
            _server.ServerOptions.BufferPoolSize = 400;
            _server.ServerOptions.BufferPoolMaxMemory = 1200;

            _server.OnMessageReceive(OnTcpMessageReceived);
            _server.OnOpened(OnOpened);
            _server.OnError(OnServerError);
            _server.OnLog(OnServerLog)
                .OnConnected(OnConnected)
                .OnDisconnect(OnDisconnected);

            _server.Run();
        }

        public void Send(long clientId, object message)
        {
            _logger.LogObject(LogLevel.Debug, "Sending {MessageType} to Player {PlayerId}.", message, clientId);
            var session = _server.AppServer.GetSession(clientId);
            if (session == null)
            {
                return;
            }

            _server.AppServer.Send(message, session);
        }

        public void Broadcast(object message)
        {
            _logger.LogObject(LogLevel.Debug, "Sending {MessageType}.", message);
            var sessions = _server.AppServer.GetOnlines();
            _server.AppServer.Send(message, sessions);
        }

        public void BroadcastExcept(long clientId, object message)
        {
            _logger.LogObject(LogLevel.Debug, "Sending {MessageType} to all EXCEPT Player={PlayerId}.", message, clientId);
            var sessions = _server.AppServer.GetOnlines().Where(s => s.ID != clientId).ToArray();
            _server.AppServer.Send(message, sessions);
        }

        public void Reset()
        {
            var sessions = _server?.AppServer?.GetOnlines() ?? [];
            _logger.LogInformation("Reset. IsActive={IsActive}, Sessions={Sessions}", IsActive, sessions.Length);
            _server?.Dispose();
            _server = null;
        }

        private void OnTcpMessageReceived(EventMessageReceiveArgs<TcpServerApp, TcpSessionToken, object> args)
        {
            var metadata = new NetworkMessageMetadata(NetworkChannelType.TCP, args.NetSession.ID, args.Message);
            OnMessageReceived?.Invoke(metadata);
        }

        private void OnOpened(IServer server)
        {
            var endpoint = server.Options?.DefaultListen?.EndPoint;
            if (endpoint == null)
            {
                _logger.LogError("Server started with null endpoint");
                return;
            }

            _logger.LogInformation("Server started. Endpoint={Endpoint}", endpoint);

            try
            {
                OnServerStarted?.Invoke(endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle OnServerStarted");
            }
        }

        private void OnServerError(IServer server, ServerErrorEventArgs args)
        {
            _logger.LogError(args.Error, args.Message);
        }

        private void OnServerLog(IServer server, ServerLogEventArgs args)
        {
            switch (args.Type)
            {
                case LogType.Error:
                    _logger.LogError(args.Message);
                    break;
                case LogType.Warring:
                    _logger.LogWarning(args.Message);
                    break;
                default:
                    _logger.LogInformation(args.Message);
                    break;
            }
        }

        private void OnDisconnected(ISession session, TcpSessionToken clientToken)
        {
            _logger.LogInformation("Client disconnected. ClientId={ClientId}", session.ID);
            try
            {
                OnClientDisconnected?.Invoke(session.ID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle OnDisconnected");
            }
        }

        private void OnConnected(ISession session, TcpSessionToken clientToken)
        {
            _logger.LogInformation("Client connected. ClientId={ClientId}", session.ID);
            try
            {
                OnClientConnected?.Invoke(session.ID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle new client connection");
            }
        }
    }
}
