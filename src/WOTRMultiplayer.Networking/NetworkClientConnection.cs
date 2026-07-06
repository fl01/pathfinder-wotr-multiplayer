using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.Abstractions.TCP;
using WOTRMultiplayer.Networking.Awaiters;
using WOTRMultiplayer.Networking.Configuration;
using WOTRMultiplayer.Networking.Consuming;

namespace WOTRMultiplayer.Networking
{
    public class NetworkClientConnection : NetworkConnectionBase, INetworkClientConnection
    {
        private readonly INetworkClient _networkClient;
        private TimeSpan _defaultAwaiterTimeout;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IAwaitableResponse>> _awaiters = new(StringComparer.OrdinalIgnoreCase);

        public bool IsConnecting => _networkClient.IsConnecting || ExternalConnectionService.IsConnecting;

        public Action<NetworkError> OnError { get; set; }

        public Action<EndPoint, string> OnConnected { get; set; }

        public NetworkClientConnection(
            ILogger<NetworkClientConnection> logger,
            IMessageConsumer messageConsumer,
            INetworkClient networkClient,
            IExternalConnectionService externalConnectionService)
            : base(logger, messageConsumer, networkClient, externalConnectionService)
        {
            _networkClient = networkClient;

            SetupEventHandlers();
        }

        public async Task ConnectAsync(string host, int port, TimeSpan awaiterTimeout)
        {
            _defaultAwaiterTimeout = awaiterTimeout;

            await _networkClient.ConnectAsync(host, port).ConfigureAwait(false);
        }

        public async Task ConnectAsync(string code, string password, ExternalServerConfiguration externalServerConfiguration, TimeSpan awaiterTimeout)
        {
            _defaultAwaiterTimeout = awaiterTimeout;

            await ExternalConnectionService.ConnectAsync(externalServerConfiguration).ConfigureAwait(false);
            await ExternalConnectionService.JoinGameAsync(code, password).ConfigureAwait(false);
        }

        public override void Reset()
        {
            base.Reset();

            _networkClient.Reset();
        }

        public async Task<T> SendAndWaitForAsync<T>(IAwaitableRequest message)
            where T : IAwaitableResponse
        {
            var taskCompletion = new TaskCompletionSource<IAwaitableResponse>();
            var timeoutTask = Task.Delay(_defaultAwaiterTimeout);

            var awaiterKey = message.GetKey();
            _awaiters.TryAdd(awaiterKey, taskCompletion);
            Broadcast(message);

            await Task.WhenAny(timeoutTask, taskCompletion.Task).ConfigureAwait(false);

            if (!taskCompletion.Task.IsCompleted)
            {
                _awaiters.TryRemove(awaiterKey, out _);
                Logger.LogWarning("Awaiter timed out. AwaiterKey={AwaiterKey}, Timeout={Timeout}", awaiterKey, _defaultAwaiterTimeout);
                return default;
            }

            var result = (T)taskCompletion.Task.Result;
            return result;
        }

        protected override void OnMessageReceived(NetworkMessageMetadata networkMessageMetadata)
        {
            // star topology = no direct connections between clients = single connection to host 
            networkMessageMetadata.PlayerId = NetworkConstants.HostPlayerId;

            // no need to deal with channels here, as clients only ever have a single connection
            if (networkMessageMetadata.Message is IAwaitableResponse awaitableResponse)
            {
                var awaiterKey = awaitableResponse.GetKey();
                if (!_awaiters.TryRemove(awaiterKey, out var taskCompletion))
                {
                    Logger.LogError("Awaiter is not configured. AwaiterKey={AwaiterKey}, Awaiters={Awaiters}", awaiterKey, _awaiters.Keys);
                    return;
                }

                Logger.LogDebug("Awaiter has been found, other handlers will be skipped. AwaiterKey={AwaiterKey}", awaiterKey);
                taskCompletion.SetResult(awaitableResponse);
                return;
            }

            base.OnMessageReceived(networkMessageMetadata);
        }

        private void SetupEventHandlers()
        {
            _networkClient.OnError = OnTcpClientError;
            _networkClient.OnConnected = OnTcpClientConnected;

            ExternalConnectionService.OnError = OnExternalConnectionError;
            ExternalConnectionService.OnPeerConnected = OnPeerConnected;
            ExternalConnectionService.OnPeerDisconnected = OnPeerDisconnected;
        }

        private void OnPeerDisconnected(int peerId)
        {
            var error = new NetworkError(NetworkErrorType.Disconnected);
            OnError?.Invoke(error);
        }

        private void OnPeerConnected(int peerId, string gameCode)
        {
            ExternalConnectionService.TerminateCoordinationAsync();

            OnConnected?.Invoke(null, gameCode);
        }

        private void OnTcpClientConnected(EndPoint endPoint)
        {
            OnConnected?.Invoke(endPoint, null);
        }

        private void OnTcpClientError(Exception exception)
        {
            // rare cases when BeetleX tcp client fails internally due to exhausted buffers or similar non-network issues
            if (exception is not SocketException socketException)
            {
                Logger.LogError(exception, "Generic network error");
                var genericError = new NetworkError(NetworkErrorType.Generic);
                OnError?.Invoke(genericError);
                return;
            }

            switch (socketException.SocketErrorCode)
            {
                case SocketError.OperationAborted:
                    Logger.LogWarning("Skipping tcp client error. SocketCode={SocketCode}", socketException.SocketErrorCode);
                    return;
                case SocketError.ConnectionReset:
                case SocketError.Success:
                    var disconnectedError = new NetworkError(NetworkErrorType.Disconnected);
                    OnError?.Invoke(disconnectedError);
                    return;
                default:
                    var socketError = new NetworkError(NetworkErrorType.SocketError)
                    {
                        SocketError = socketException.SocketErrorCode
                    };
                    OnError?.Invoke(socketError);
                    return;
            }
        }

        private void OnExternalConnectionError(NetworkError error)
        {
            OnError?.Invoke(error);
        }
    }
}
