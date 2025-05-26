using System.Collections.Generic;
using System.Linq;
using System.Net;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Networking;
using WOTRMultiplayer.Networking.Messages.System;

namespace WOTRMultiplayer
{
    public class MultiplayerHost
    {
        private readonly NetworkServer _networkServer;

        private readonly List<NetworkPlayer> _playersList = [];
        private readonly object _actionlock = new();

        public bool IsActive => _networkServer.IsActive;

        public MultiplayerHost(NetworkServer networkServer)
        {
            _networkServer = networkServer;

            RegisterHandlers();
        }

        public void Start(MultiplayerSettings settings)
        {
            if (_networkServer.IsActive)
            {
                _networkServer.Dispose();
            }

            _networkServer.Start(settings.NetworkInterfaceBinding, settings.Port);
        }

        public void Reset()
        {
            lock (_actionlock)
            {
                _playersList.Clear();
            }

            _networkServer.Dispose();
        }

        private void RegisterHandlers()
        {
            _networkServer.OnClientConnected = OnPlayerConnected;
            _networkServer.OnClientDisconnected = OnPlayerDisconnected;
            _networkServer.OnServerStarted = OnServerStarted;

            _networkServer
                .Register<NetworkClientNameResponse>(OnNetworkClientNameResponse);
        }

        private void OnServerStarted(EndPoint point)
        {
            // update ui
        }

        private void OnPlayerConnected(long clientId)
        {
            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(clientId);
                if (existingPlayer != null)
                {
                    // warn
                    return;
                }

                var player = new NetworkPlayer(clientId);
                _playersList.Add(player);

                _networkServer.Send(clientId, new NetworkClientNameRequest());
            }
        }

        private void OnPlayerDisconnected(long clientId)
        {
            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(clientId);
                if (existingPlayer == null)
                {
                    // warn
                    return;
                }

                _playersList.Remove(existingPlayer);

                // UPDATE UI
            }
        }

        private void OnNetworkClientNameResponse(long clientId, NetworkClientNameResponse response)
        {
            var existingPlayer = GetPlayer(clientId);
            if (existingPlayer == null)
            {
                // warn
                return;
            }

            if (string.IsNullOrEmpty(response.Name))
            {
                // warn
                // generate new or disconnect?
                return;
            }

            existingPlayer.Name = response.Name;
            // UPDATE UI
        }

        private NetworkPlayer GetPlayer(long playerId)
        {
            return _playersList.FirstOrDefault(p => p.Id == playerId);
        }
    }
}
