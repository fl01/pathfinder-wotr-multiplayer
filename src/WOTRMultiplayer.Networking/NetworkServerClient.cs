using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using BeetleX;
using BeetleX.Clients;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Networking
{
    public class NetworkServerClient : INetworkServerClient
    {
        private AsyncTcpClient _client;
        private readonly ConcurrentDictionary<Type, Action<object>> _handlers = new();
        private readonly ILogger<NetworkServerClient> _logger;

        public bool IsActive => _client?.IsConnected ?? false;

        public NetworkServerClient(ILogger<NetworkServerClient> logger)
        {
            _logger = logger;
        }

        public async Task ConnectAsync(string host, int port)
        {
            _client = SocketFactory.CreateClient<AsyncTcpClient>(new Messages.ProtobufClientPacket(), host, port);
            _client.PacketReceive = OnPackedReceived;
            var status = await _client.Connect();
        }

        public INetworkServerClient Register<TMessage>(Action<TMessage> handler)
            where TMessage : class
        {
            _logger.LogInformation("Registering handler. Type={type}", typeof(TMessage));
            _handlers.TryAdd(typeof(TMessage), message => handler((TMessage)message));

            return this;
        }

        public Task SendAsync(object message)
        {
            return _client.Send(message);
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing");
            _client?.Dispose();
        }

        private void OnPackedReceived(IClient client, object message)
        {
            var type = message.GetType();
            if (!_handlers.TryGetValue(type, out var handler))
            {
                _logger.LogWarning("Handler is not configured. Type={type}", type);
                return;
            }

            try
            {
                handler(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle message");
            }
        }
    }
}
