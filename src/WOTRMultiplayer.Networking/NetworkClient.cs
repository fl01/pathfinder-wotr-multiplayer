using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using BeetleX.Clients;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Logging.Extensions;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Awaiters;
using WOTRMultiplayer.Networking.Consuming;

namespace WOTRMultiplayer.Networking
{
    public class NetworkClient : INetworkClient
    {
        private TimeSpan _defaultAwaiterTimeout;

        private ITcpClient _client;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IAwaitableResponse>> _awaiters = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<NetworkClient> _logger;
        private readonly ITcpClientFactory _tcpClientFactory;
        private readonly IMessageConsumer _messageConsumer;

        public Action<Exception> OnError { get; set; }

        public Action<EndPoint> OnConnected { get; set; }

        public bool IsActive => (_client?.IsConnected ?? false);

        public bool IsConnecting { get; private set; } = false;

        public NetworkClient(
            ILogger<NetworkClient> logger,
            ITcpClientFactory tcpClientFactory,
            IMessageConsumer messageConsumer)
        {
            _logger = logger;
            _tcpClientFactory = tcpClientFactory;
            _messageConsumer = messageConsumer;
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

        public INetworkReceiver On<TMessage>(Action<long, TMessage> handler, MessageHandlerPriority priority = MessageHandlerPriority.Default)
            where TMessage : class
        {
            _messageConsumer.On<TMessage>(handler, MessageHandlerPriority.Default);
            return this;
        }

        public void Send(object message)
        {
            _logger.LogObject(LogLevel.Information, "Sending {MessageType}.", message);
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
            _client?.Dispose();
            _messageConsumer.Reset();
        }

        private void OnPackedReceived(IClient client, object message)
        {
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

            _messageConsumer.Enqueue(new NetworkMessageMetadata(NetworkingConsts.HostPlayerId, message));
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
