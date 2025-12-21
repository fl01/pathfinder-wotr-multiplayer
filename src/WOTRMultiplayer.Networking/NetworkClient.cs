using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using BeetleX.Clients;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Awaiters;

namespace WOTRMultiplayer.Networking
{
    public class NetworkClient : INetworkClient
    {
        private TimeSpan _defaultAwaiterTimeout;

        private ITcpClient _client;
        private readonly ConcurrentDictionary<Type, Action<long, object>> _handlers = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _awaiters = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<NetworkClient> _logger;
        private readonly ITcpClientFactory _tcpClientFactory;

        public Action<Exception> OnError { get; set; }

        public Action<EndPoint> OnConnected { get; set; }

        public bool IsActive => (_client?.IsConnected ?? false);

        public bool IsConnecting { get; private set; } = false;

        public NetworkClient(
            ILogger<NetworkClient> logger,
            ITcpClientFactory tcpClientFactory)
        {
            _logger = logger;
            _tcpClientFactory = tcpClientFactory;
        }

        public async Task ConnectAsync(string host, int port, TimeSpan awaiterTimeout)
        {
            _defaultAwaiterTimeout = awaiterTimeout;

            _client = _tcpClientFactory.Create(host, port);

            _client.ClientError = OnClientError;
            _client.Connected = OnClientConnected;
            _client.PacketReceive = OnPackedReceived;
            IsConnecting = true;
            await _client.Connect();
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

        public INetworkReceiver On<TMessage>(Action<long, TMessage> handler)
            where TMessage : class
        {
            var messageType = typeof(TMessage);
            _logger.LogDebug("Adding message handler. Type={Type}", messageType);
            if (!_handlers.TryAdd(messageType, (receivedFrom, message) => handler(receivedFrom, (TMessage)message)))
            {
                _logger.LogError("Duplicate message handler detected. MessageType={MessageType}", messageType);
            }

            return this;
        }

        public void Send(object message)
        {
            _client.Send(message).Wait();
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
            _handlers.Clear();
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
                handler(NetworkingConsts.HostPlayerId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle message. MessageType={MessageType}", messageType.Name);
            }
        }
    }
}
