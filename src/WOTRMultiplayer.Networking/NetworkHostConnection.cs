using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.Abstractions.TCP;
using WOTRMultiplayer.Networking.Channels;
using WOTRMultiplayer.Networking.Configuration;
using WOTRMultiplayer.Networking.Consuming;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking
{
    public class NetworkHostConnection : NetworkConnectionBase, INetworkHostConnection
    {
        private readonly INetworkServer _networkServer;
        private int _nextClientId = 0;
        private readonly ConcurrentDictionary<long, ClientConnection> _playerToClient = [];
        private readonly ConcurrentDictionary<NetworkChannelType, ConcurrentDictionary<long, long>> _clientToPlayer = [];

        public Action<long> OnPlayerDisconnected { get; set; }

        public Action<long> OnPlayerConnected { get; set; }

        public Action<EndPoint> OnLocalServerStarted { get; set; }

        public Action<bool?, string> OnExternalConnectivityUpdated { get; set; }

        public NetworkHostConnection(
            ILogger<NetworkHostConnection> logger,
            IMessageConsumer messageConsumer,
            INetworkServer networkServer,
            IExternalConnectionService externalConnectionService)
            : base(logger, messageConsumer, networkServer, externalConnectionService)
        {
            _networkServer = networkServer;

            SetupEventHandlers();
        }

        public void EnableExternalConnections(ExternalServerConfiguration externalServerConfiguration)
        {
            ExternalConnectionService.ConnectAsync(externalServerConfiguration);
            OnExternalConnectivityUpdated?.Invoke(null, null);
        }

        public void HostTcpServer(NetworkServerConfiguration serverConfiguration)
        {
            _networkServer.Start(serverConfiguration);
        }

        public override void Reset()
        {
            base.Reset();

            _nextClientId = 0;
            _playerToClient.Clear();
            _networkServer.Reset();
        }

        public void Send(long playerId, object message)
        {
            if (!TryGetChannelInfo(playerId, out var channel, out var clientId))
            {
                Logger.LogError("Unable to send because target player is not registered correctly. PlayerId={PlayerId}", playerId);
                return;
            }

            switch (channel)
            {
                case NetworkChannelType.TCP:
                    _networkServer.Send(clientId, message);
                    break;
                case NetworkChannelType.P2P:
                    ExternalConnectionService.Send(clientId, message);
                    break;
                default:
                    Logger.LogError("Unable to send to unknown channel type. ChannelType={ChannelType}", channel);
                    break;
            }
        }

        public void BroadcastExcept(long playerId, object message)
        {
            if (!TryGetChannelInfo(playerId, out var channel, out var clientId))
            {
                Logger.LogError("Unable to broadcast because excluded player is not registered. PlayerId={PlayerId}", playerId);
                return;
            }

            BroadcastExcept(channel, clientId, message);
        }

        private void BroadcastExcept(NetworkChannelType channel, long clientId, object message)
        {
            switch (channel)
            {
                case NetworkChannelType.TCP:
                    _networkServer.BroadcastExcept(clientId, message);
                    ExternalConnectionService.Broadcast(message);
                    break;
                case NetworkChannelType.P2P:
                    _networkServer.Broadcast(message);
                    ExternalConnectionService.BroadcastExcept(clientId, message);
                    break;
                default:
                    Logger.LogError("Unable to broadcast to unknown channel type. ChannelType={ChannelType}", channel);
                    break;
            }
        }

        protected override void OnMessageReceived(NetworkMessageMetadata networkMessageMetadata)
        {
            if (networkMessageMetadata.Message is IForwardableMessage)
            {
                try
                {
                    BroadcastExcept(networkMessageMetadata.ChannelType, networkMessageMetadata.ClientId, networkMessageMetadata.Message);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error while forwarding message. ChannelType={ChannelType}, ClientId={ClientId}, MessageType={MessageType}", networkMessageMetadata.ChannelType, networkMessageMetadata.ClientId, networkMessageMetadata.Message.GetType().Name);
                }
            }

            if (!TryGetPlayerInfo(networkMessageMetadata.ChannelType, networkMessageMetadata.ClientId, out var playerId))
            {
                Logger.LogError("Unable to find player id for specified client. ChannelType={ChannelType}, ClientId={ClientId}", networkMessageMetadata.ChannelType, networkMessageMetadata.ClientId);
                return;
            }

            networkMessageMetadata.PlayerId = playerId;

            base.OnMessageReceived(networkMessageMetadata);
        }

        private void SetupEventHandlers()
        {
            _networkServer.OnServerStarted = endpoint => OnLocalServerStarted?.Invoke(endpoint);

            _networkServer.OnClientConnected = clientId => OnClientConnected(NetworkChannelType.TCP, clientId);
            ExternalConnectionService.OnPeerConnected = clientId => OnClientConnected(NetworkChannelType.P2P, clientId);

            _networkServer.OnClientDisconnected = clientId => OnClientDisconnected(NetworkChannelType.TCP, clientId);
            ExternalConnectionService.OnPeerDisconnected = clientId => OnClientDisconnected(NetworkChannelType.P2P, clientId);

            ExternalConnectionService.OnConnected = OnConnected;
            ExternalConnectionService.OnError = OnError;
            ExternalConnectionService.OnGameCodeChanged = OnGameCodeChanged;
        }

        private void OnGameCodeChanged(string code)
        {
            OnExternalConnectivityUpdated?.Invoke(true, code);
        }

        private void OnError()
        {
            OnExternalConnectivityUpdated?.Invoke(false, null);
        }

        private void OnConnected()
        {
            OnExternalConnectivityUpdated?.Invoke(true, null);
        }

        private bool TryGetChannelInfo(long playerId, out NetworkChannelType channel, out long clientId)
        {
            if (!_playerToClient.TryGetValue(playerId, out var connection))
            {
                channel = default;
                clientId = default;
                return false;
            }

            channel = connection.ChannelType;
            clientId = connection.ClientId;
            return true;
        }

        private bool TryGetPlayerInfo(NetworkChannelType channelType, long clientId, out long playerId)
        {
            if (!_clientToPlayer.TryGetValue(channelType, out var channel))
            {
                playerId = default;
                return false;
            }

            if (!channel.TryGetValue(clientId, out var player))
            {
                playerId = default;
                return false;
            }

            playerId = player;
            return true;
        }

        private void OnClientDisconnected(NetworkChannelType channelType, long clientId)
        {
            if (!TryGetPlayerInfo(channelType, clientId, out var playerId))
            {
                Logger.LogWarning("Disconnected player has not been registered yet. Channel={Channel}, ChannelClientId={ChannelClientId}", channelType, clientId);
                return;
            }

            Logger.LogInformation("Player has been disconnected. Channel={Channel}, ChannelClientId={ChannelClientId}, PlayerId={PlayerId}", channelType, clientId, playerId);
            OnPlayerDisconnected?.Invoke(playerId);
        }

        private void OnClientConnected(NetworkChannelType channelType, long clientId)
        {
            var playerId = Interlocked.Increment(ref _nextClientId);
            var connection = new ClientConnection { ChannelType = channelType, ClientId = clientId };
            _playerToClient.TryAdd(playerId, connection);

            var channel = _clientToPlayer.GetOrAdd(channelType, []);
            channel.TryAdd(clientId, playerId);
            Logger.LogInformation("Player Id has been assigned. Channel={Channel}, ChannelClientId={ChannelClientId}, PlayerId={PlayerId}", channelType, clientId, playerId);

            OnPlayerConnected?.Invoke(playerId);
        }

        private class ClientConnection
        {
            public long ClientId { get; set; }

            public NetworkChannelType ChannelType { get; set; }
        }
    }
}
