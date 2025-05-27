using System.Collections.Generic;
using System.Linq;
using System.Net;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Lobby;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerHost : IMultiplayerHost
    {
        private readonly INetworkServer _networkServer;
        private readonly ILobbyWindowController _lobbyWindowController;

        private readonly List<NetworkPlayer> _playersList = [];
        private readonly object _actionlock = new();

        public bool IsActive => _networkServer.IsActive;

        public MultiplayerHost(INetworkServer networkServer, ILobbyWindowController lobbyWindowController)
        {
            _networkServer = networkServer;
            _lobbyWindowController = lobbyWindowController;

            RegisterHandlers();
        }

        public void Start(MultiplayerSettings settings)
        {
            if (_networkServer.IsActive)
            {
                _networkServer.Dispose();
            }

            _networkServer.Start();
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
                .Register<PlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
                .Register<PlayerNameResponse>(OnPlayerNameResponse)
                ;
        }

        private void OnPlayerReadyStatusChanged(long playerId, PlayerReadyStatusChanged readyStatusChanged)
        {
            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(playerId);
                if (existingPlayer == null)
                {
                    // warn
                    return;
                }

                existingPlayer.IsReady = readyStatusChanged.IsReady;

                // new event maybe to notify all clients
                //readyStatusChanged.PlayerId = playerId;
                //_networkServer.SendAllExcept(playerId, readyStatusChanged);
                // update UI
                var allAreReady = _playersList.All(p => p.IsReady);
            }

        }

        private void OnPlayerNameResponse(long playerId, PlayerNameResponse response)
        {
            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(playerId);
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
                _lobbyWindowController.UpdatePlayers(_playersList);
            // send updates to other clients
            }
        }

        private void OnPlayerConnected(long playerId)
        {
            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(playerId);
                if (existingPlayer != null)
                {
                    // warn
                    return;
                }

                var player = new NetworkPlayer(playerId);
                _playersList.Add(player);

                _networkServer.Send(playerId, new PlayerNameRequest());
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

                // send updates to other clients
                // UPDATE UI
            }
        }

        private void OnServerStarted(EndPoint point)
        {
            // update ui
        }

        private NetworkPlayer GetPlayer(long playerId)
        {
            return _playersList.FirstOrDefault(p => p.Id == playerId);
        }
    }
}
