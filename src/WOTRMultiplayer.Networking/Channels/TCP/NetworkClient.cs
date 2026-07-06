using System;
using System.Net;
using System.Threading.Tasks;
using BeetleX.Clients;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Logging.Extensions;
using WOTRMultiplayer.Networking.Abstractions.TCP;
using WOTRMultiplayer.Networking.Consuming;

namespace WOTRMultiplayer.Networking.Channels.TCP
{
    public class NetworkClient : INetworkClient
    {
        private ITcpClient _client;
        private readonly ILogger<NetworkClient> _logger;
        private readonly ITcpFactory _tcpClientFactory;

        public Action<Exception> OnError { get; set; }

        public Action<EndPoint> OnConnected { get; set; }

        public Action<NetworkMessageMetadata> OnMessageReceived { get; set; }

        public bool IsActive => _client?.IsConnected ?? false;

        public bool IsConnecting { get; private set; } = false;

        public NetworkClient(
            ILogger<NetworkClient> logger,
            ITcpFactory tcpClientFactory)
        {
            _logger = logger;
            _tcpClientFactory = tcpClientFactory;
        }

        public async Task ConnectAsync(string host, int port)
        {
            _client = _tcpClientFactory.CreateClient(host, port);

            _client.ClientError = OnClientError;
            _client.Connected = OnClientConnected;
            _client.PacketReceive = OnPackedReceived;

            IsConnecting = true;
            await _client.Connect();
        }

        public void Broadcast(object message)
        {
            _logger.LogObject(LogLevel.Debug, "Sending {MessageType}.", message);
            var sender = _client?.SendAsync(message) ?? Task.CompletedTask;
            sender.Wait();
        }

        public void Reset()
        {
            _logger.LogInformation("Resetting. IsActive={IsActive}", IsActive);
            IsConnecting = false;
            _client?.Dispose();
        }

        private void OnPackedReceived(IClient client, object message)
        {
            var metadata = new NetworkMessageMetadata(NetworkChannelType.TCP, NetworkConstants.HostPlayerId, message);
            OnMessageReceived?.Invoke(metadata);
        }

        private void OnClientConnected(IClient client)
        {
            IsConnecting = false;

            var endpoint = client.Socket.RemoteEndPoint;
            OnConnected?.Invoke(endpoint);
        }

        private void OnClientError(IClient client, ClientErrorArgs args)
        {
            IsConnecting = false;

            OnError?.Invoke(args.Error);
        }
    }
}
