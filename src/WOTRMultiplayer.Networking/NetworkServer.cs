using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BeetleX;
using BeetleX.EventArgs;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Logging.Extensions;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Awaiters;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking
{
    public class NetworkServer : INetworkServer
    {
        private TimeSpan _defaultAwaiterTimeout;

        private ServerBuilder<NetworkServerApp, NetworkConnectionToken, ProtobufPacket> _server;
        private ServerBuilder<NetworkServerApp, NetworkConnectionToken, ProtobufPacket> Server => _server ??= new ServerBuilder<NetworkServerApp, NetworkConnectionToken, ProtobufPacket>();
        private readonly ILogger<NetworkServer> _logger;
        private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, TaskCompletionSource<IAwaitableResponse>>> _awaiters = new();

        public Action<long> OnClientConnected { get; set; }

        public Action<long> OnClientDisconnected { get; set; }

        public Action<EndPoint> OnServerStarted { get; set; }

        public bool IsActive => Server.AppServer?.Status == ServerStatus.Start;

        public NetworkServer(ILogger<NetworkServer> logger)
        {
            _logger = logger;
        }

        public INetworkReceiver On<TMessage>(Action<long, TMessage> messageHandler)
            where TMessage : class
        {
            _logger.LogDebug("Adding message handler. Type={Type}", typeof(TMessage).Name);
            Server.OnMessageReceive<TMessage>(args => OnHandleMessage(args, messageHandler));
            return this;
        }

        public void Start(int hostPortRangeStart, int hostPortRangeEnd, TimeSpan awaiterTimeout)
        {
            _defaultAwaiterTimeout = awaiterTimeout;

            Server.ServerOptions.DefaultListen.StartRegionPort = hostPortRangeStart;
            Server.ServerOptions.DefaultListen.EndRegionPort = hostPortRangeEnd;
            Server.ServerOptions.BufferSize = 1024 * 64;
            Server.ServerOptions.BufferPoolGroups = 12;
            Server.OnMessageReceive(OnMissingMessageHandler);
            Server.OnOpened(OnOpened);
            Server.OnLog(OnServerLog)
                .OnConnected(OnConnected)
                .OnDisconnect(OnDisconnected);

            Server.Run();
        }

        public void Send(long clientId, object message)
        {
            var session = Server.AppServer.GetSession(clientId);
            if (session == null)
            {
                _logger.LogWarning("Client doesn't exist. ClientId={ClientId}", clientId);
                return;
            }

            Server.AppServer.Send(message, session);
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

        public void SendAll(object message)
        {
            var sessions = Server.AppServer.GetOnlines();
            Server.AppServer.Send(message, sessions);
        }

        public void SendAllExcept(long clientId, object message)
        {
            var sessions = Server.AppServer.GetOnlines().Where(s => s.ID != clientId).ToArray();
            Server.AppServer.Send(message, sessions);
        }

        private void OnMissingMessageHandler(EventMessageReceiveArgs<NetworkServerApp, NetworkConnectionToken, object> args)
        {
            _logger.LogError("Missing message handler. PlayerId={PlayerId}, MessageType={MessageType}", args.Session.Id, args.Message.GetType().Name);
        }

        private void OnHandleMessage<TMessage>(EventMessageReceiveArgs<NetworkServerApp, NetworkConnectionToken, TMessage> args, Action<long, TMessage> handler)
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

            if (handler == null)
            {
                _logger.LogWarning("Skipping null handler. MessageType={MessageType}", typeof(TMessage).Name);
                return;
            }

            try
            {
                _logger.LogObject(LogLevel.Information, "Received {MessageType}. ReceivedFrom={ReceivedFrom}", args.Message, args.Session.Id);
                handler(args.NetSession.ID, args.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle message. MessageType={MessageType}", typeof(TMessage).Name);
                throw;
            }
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

        private void OnServerLog(IServer server, ServerLogEventArgs args)
        {
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

        public void Reset()
        {
            _logger.LogInformation("Reset. IsActive={IsActive}, ClientsCount={ClientsCount}", IsActive, _server?.AppServer?.GetOnlines()?.Length ?? 0);
            _server?.Dispose();
            _server = null;
        }
    }
}
