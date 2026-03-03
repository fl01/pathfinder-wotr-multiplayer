using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BeetleX;
using BeetleX.EventArgs;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Awaiters;
using WOTRMultiplayer.Networking.Consuming;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking
{
    public class NetworkServer : INetworkServer
    {
        private TimeSpan _defaultAwaiterTimeout;
        private ServerBuilder<NetworkServerApp, NetworkConnectionToken, ProtobufPacket> _server;

        private readonly ILogger<NetworkServer> _logger;
        private readonly IMessageConsumer _messageConsumer;
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, TaskCompletionSource<IAwaitableResponse>>> _awaiters = new();

        public Action<long> OnClientConnected { get; set; }

        public Action<long> OnClientDisconnected { get; set; }

        public Action<EndPoint> OnServerStarted { get; set; }

        public bool IsActive => _server?.AppServer?.Status == ServerStatus.Start;

        public NetworkServer(
            ILogger<NetworkServer> logger,
            IMessageConsumer messageConsumer)
        {
            _logger = logger;
            _messageConsumer = messageConsumer;
        }

        public INetworkReceiver On<TMessage>(Action<long, TMessage> messageHandler, MessageHandlerPriority priority = MessageHandlerPriority.Default)
            where TMessage : class
        {
            _messageConsumer.On<TMessage>(messageHandler, priority);
            return this;
        }

        public void Start(int hostPortRangeStart, int hostPortRangeEnd, TimeSpan awaiterTimeout)
        {
            _defaultAwaiterTimeout = awaiterTimeout;

            if (_server != null)
            {
                Reset();
            }

            _server = new ServerBuilder<NetworkServerApp, NetworkConnectionToken, ProtobufPacket>();
            _server.ServerOptions.DefaultListen.StartRegionPort = hostPortRangeStart;
            _server.ServerOptions.DefaultListen.EndRegionPort = hostPortRangeEnd;
            _server.ServerOptions.BufferSize = 1024 * 32;
            _server.ServerOptions.BufferPoolSize = 400;
            _server.ServerOptions.BufferPoolMaxMemory = 1200;

            _server.OnMessageReceive(OnMessageReceived);
            _server.OnOpened(OnOpened);
            _server.OnError(OnServerError);
            _server.OnLog(OnServerLog)
                .OnConnected(OnConnected)
                .OnDisconnect(OnDisconnected);

            _server.Run();
        }

        public void Send(long clientId, object message)
        {
            var session = _server.AppServer.GetSession(clientId);
            if (session == null)
            {
                _logger.LogWarning("Client doesn't exist. ClientId={ClientId}", clientId);
                return;
            }

            _server.AppServer.Send(message, session);
        }

        public async Task<T> SendAndWaitForAsync<T>(long clientId, IAwaitableRequest message)
            where T : IAwaitableResponse
        {
            var taskCompletion = new TaskCompletionSource<IAwaitableResponse>();
            var timeoutTask = Task.Delay(_defaultAwaiterTimeout);
            var awaiterKey = message.GetKey();

            AddAwaiter(clientId, awaiterKey, taskCompletion);
            Send(clientId, message);

            await Task.WhenAny(timeoutTask, taskCompletion.Task).ConfigureAwait(false);

            if (!taskCompletion.Task.IsCompleted)
            {
                RemoveAwaiter(clientId, awaiterKey);
                _logger.LogWarning("Awaiter has been failed due to timeout. PlayerId={PlayerId}, AwaiterKey={AwaiterKey}, Timeout={Timeout}", clientId, awaiterKey, _defaultAwaiterTimeout);
                return default;
            }

            var result = (T)taskCompletion.Task.Result;
            return result;
        }

        public void SendAll(object message)
        {
            var sessions = _server.AppServer.GetOnlines();
            _server.AppServer.Send(message, sessions);
        }

        public void SendAllExcept(long clientId, object message)
        {
            var sessions = _server.AppServer.GetOnlines().Where(s => s.ID != clientId).ToArray();
            _server.AppServer.Send(message, sessions);
        }

        public void Reset()
        {
            var sessions = _server?.AppServer?.GetOnlines() ?? [];
            _logger.LogInformation("Reset. IsActive={IsActive}, Sessions={Sessions}", IsActive, sessions.Length);
            _server?.Dispose();
            _server = null;
            _messageConsumer.Reset();
        }

        private void OnMessageReceived(EventMessageReceiveArgs<NetworkServerApp, NetworkConnectionToken, object> args)
        {
            var clientId = args.NetSession.ID;
            if (args.Message is IAwaitableResponse awaitable)
            {
                var awaiterKey = awaitable.GetKey();
                if (!_awaiters.TryGetValue(clientId, out var clientAwaiters) || !clientAwaiters.TryRemove(awaiterKey, out var awaiter))
                {
                    _logger.LogError("Received AwaitableResponse, but awaiter is not configured. ClientId={ClientId}, AwaiterKey={AwaiterKey}", clientId, awaiterKey);
                    return;
                }

                _logger.LogDebug("Awaiter has been found, other handlers will be skipped. ClientId={ClientId}, AwaiterKey={AwaiterKey}", clientId, awaiterKey);
                awaiter.SetResult(awaitable);
                return;
            }

            _messageConsumer.Enqueue(new NetworkMessageMetadata(args.NetSession.ID, args.Message));
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

        private void OnDisconnected(ISession session, NetworkConnectionToken clientToken)
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

        private void OnConnected(ISession session, NetworkConnectionToken clientToken)
        {
            _logger.LogInformation("Client connected. ClientId={ClientId}", session.ID);
            try
            {
                OnClientConnected?.Invoke(session.ID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle OnClientConnected");
            }
        }

        private void AddAwaiter(long clientId, string awaiterKey, TaskCompletionSource<IAwaitableResponse> task)
        {
            _awaiters.AddOrUpdate(clientId, key =>
            {
                var kv = new KeyValuePair<string, TaskCompletionSource<IAwaitableResponse>>(awaiterKey, task);
                return new ConcurrentDictionary<string, TaskCompletionSource<IAwaitableResponse>>([kv], StringComparer.OrdinalIgnoreCase);
            },
            (key, existing) =>
            {
                existing.TryAdd(awaiterKey, task);
                return existing;
            });
        }

        private void RemoveAwaiter(long clientId, string awaiterKey)
        {
            if (_awaiters.TryGetValue(clientId, out var clientAwaiters))
            {
                clientAwaiters.TryRemove(awaiterKey, out _);
            }
        }
    }
}
