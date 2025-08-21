using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using BeetleX;
using BeetleX.Clients;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Awaiters;

namespace WOTRMultiplayer.Networking
{
    public class NetworkServerClient : INetworkServerClient
    {
        private AsyncTcpClient _client;
        private readonly ConcurrentDictionary<Type, Action<object>> _handlers = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _awaiters = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<NetworkServerClient> _logger;
        private readonly TimeSpan _defaultAwaiterTimeout = TimeSpan.FromMinutes(1);

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

        public INetworkServerClient On<TMessage>(Action<TMessage> handler)
            where TMessage : class
        {
            _logger.LogDebug("Adding message handler. Type={Type}", typeof(TMessage));
            _handlers.TryAdd(typeof(TMessage), message => handler((TMessage)message));

            return this;
        }

        public void Send(object message)
        {
            _client.Send(message).Wait();
        }

        public Task SendAsync(object message)
        {
            return _client.Send(message);
        }

        public T SendAndWaitFor<T>(object message)
            where T : class
        {
            if (message is not IAwaitableMessage awaitableMessage || !typeof(IAwaitableMessage).IsAssignableFrom(typeof(T)))
            {
                throw new InvalidOperationException("Both message/response should implement IAwaitableMessage");
            }

            var taskCompletion = new TaskCompletionSource<object>();
            var timeoutTask = Task.Delay(_defaultAwaiterTimeout);

            var awaiterKey = awaitableMessage.GetKey();
            _awaiters.TryAdd(awaiterKey, taskCompletion);
            _client.Send(message).Wait();

            Task.WaitAny(timeoutTask, taskCompletion.Task);

            if (!taskCompletion.Task.IsCompleted)
            {
                _awaiters.TryRemove(awaiterKey, out _);
                _logger.LogWarning("Awaiter has been failed due to timeout. AwaiterKey={AwaiterKey}, Timeout={Timeout}", awaiterKey, _defaultAwaiterTimeout);
                return null;
            }

            return taskCompletion.Task.Result as T;
        }

        public void Reset()
        {
            _logger.LogInformation("Resetting. IsActive={IsActive}", IsActive);
            IsConnecting = false;
            _client?.Dispose();
        }

        private void OnPackedReceived(IClient client, object message)
        {
            var messageType = message.GetType();
            if (message is IAwaitableMessage awaitableMessage && _awaiters.TryRemove(awaitableMessage.GetKey(), out var taskCompletion))
            {
                _logger.LogDebug("Awaiter has been found, other handlers will be skipped. AwaiterKey={AwaiterKey}", awaitableMessage.GetKey());
                taskCompletion.SetResult(message);
                return;
            }

            if (!_handlers.TryGetValue(messageType, out var handler))
            {
                _logger.LogWarning("Handler is not configured. Type={Type}", messageType);
                return;
            }

            try
            {
                handler(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle message. MessageType={MessageType}", messageType.Name);
            }
        }
    }
}
