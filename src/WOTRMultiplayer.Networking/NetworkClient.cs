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
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IAwaitableResponse>> _awaiters = new(StringComparer.OrdinalIgnoreCase);
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
            _client.SendAsync(message).Wait();
        }

        public async Task<T> SendAndWaitForAsync<T>(IAwaitableRequest message)
            where T : IAwaitableResponse
        {
            var taskCompletion = new TaskCompletionSource<IAwaitableResponse>();
            var timeoutTask = Task.Delay(_defaultAwaiterTimeout);

            var awaiterKey = message.GetKey();
            _awaiters.TryAdd(awaiterKey, taskCompletion);
            await _client.SendAsync(message).ConfigureAwait(false);

            await Task.WhenAny(timeoutTask, taskCompletion.Task).ConfigureAwait(false);

            if (!taskCompletion.Task.IsCompleted)
            {
                _awaiters.TryRemove(awaiterKey, out _);
                _logger.LogWarning("Awaiter has been failed due to timeout. AwaiterKey={AwaiterKey}, Timeout={Timeout}", awaiterKey, _defaultAwaiterTimeout);
                return default;
            }

            var result = (T)taskCompletion.Task.Result;
            return result;
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
            if (message is IAwaitableResponse awaitableMessage)
            {
                var awaiterKey = awaitableMessage.GetKey();
                if (!_awaiters.TryRemove(awaiterKey, out var taskCompletion))
                {
                    _logger.LogError("Received AwaitableResponse, but awaiter is not configured. AwaiterKey={AwaiterKey}, Awaiters={Awaiters}", awaiterKey, _awaiters.Keys);
                    return;
                }

                _logger.LogDebug("Awaiter has been found, other handlers will be skipped. AwaiterKey={AwaiterKey}", awaiterKey);
                taskCompletion.SetResult(awaitableMessage);
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
