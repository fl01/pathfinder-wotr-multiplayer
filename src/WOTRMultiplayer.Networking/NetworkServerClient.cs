using System;
using System.Collections.Concurrent;
using System.Net;
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
        private readonly ConcurrentDictionary<Type, TaskCompletionSource<object>> _awaiters = new();
        private readonly ILogger<NetworkServerClient> _logger;
        private readonly TimeSpan _defaultAwaiterTimeout = TimeSpan.FromSeconds(30);

        public Action<Exception> OnError { get; set; }
        public Action<EndPoint> OnConnected { get; set; }

        public bool IsActive => (_client?.IsConnected ?? false);
        public bool IsConnecting { get; private set; } = false;

        public NetworkServerClient(ILogger<NetworkServerClient> logger)
        {
            _logger = logger;
        }

        public async Task ConnectAsync(string host, int port)
        {
            _client = SocketFactory.CreateClient<AsyncTcpClient>(new Messages.ProtobufClientPacket(), host, port);
            _client.ClientError = OnClientError;
            _client.Connected = OnClientConnected;
            _client.PacketReceive = OnPackedReceived;
            IsConnecting = true;
            var status = await _client.Connect();
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

            _logger.LogError(args.Error, "Network client error");
            OnError?.Invoke(args.Error);
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

        public async Task<T> SendAndWaitForAsync<T>(object message)
            where T : class
        {
            var taskCompletion = new TaskCompletionSource<object>();
            var timeoutTask = Task.Delay(_defaultAwaiterTimeout);

            _awaiters.TryAdd(typeof(T), taskCompletion);
            await _client.Send(message);

            Task.WaitAny(timeoutTask, taskCompletion.Task);

            if (!taskCompletion.Task.IsCompleted)
            {
                _awaiters.TryRemove(typeof(T), out _);
                _logger.LogWarning("Awaiter has been failed due to timeout. Type={type}, Timeout={timeout}", typeof(T), _defaultAwaiterTimeout);
                return null;
            }

            return taskCompletion.Task.Result as T;
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing");
            IsConnecting = false;
            _client?.Dispose();
        }

        private void OnPackedReceived(IClient client, object message)
        {
            var type = message.GetType();
            if (_awaiters.TryRemove(type, out var taskCompletion))
            {
                _logger.LogInformation("Awaiter has been found, other handlers will be skipped. MessageType={type}", type);
                taskCompletion.SetResult(message);
                return;
            }

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
                _logger.LogError(ex, "Unable to handle message. MessageType={messageType}", type.Name);
            }
        }
    }
}
