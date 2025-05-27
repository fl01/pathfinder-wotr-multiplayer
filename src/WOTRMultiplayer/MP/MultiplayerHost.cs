using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Lobby;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerHost : IMultiplayerHost
    {
        private readonly ILogger<MultiplayerHost> _logger;
        private readonly INetworkServer _networkServer;
        private readonly ILobbyWindowController _lobbyWindowController;
        private readonly List<NetworkPlayer> _playersList = [];
        private readonly object _actionlock = new();

        public bool IsActive => _networkServer.IsActive;

        public MultiplayerHost(
            ILogger<MultiplayerHost> logger,
            INetworkServer networkServer,
            ILobbyWindowController lobbyWindowController)
        {
            _logger = logger;
            _networkServer = networkServer;
            _lobbyWindowController = lobbyWindowController;
        }

        public void Start(MultiplayerSettings settings)
        {
            if (_networkServer.IsActive)
            {
                _networkServer.Dispose();
            }

            RegisterHandlers();

            _networkServer.Start();
        }

        public void Stop()
        {
            _logger.LogInformation("Stop");

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
                _lobbyWindowController.UpdatePlayers(_playersList);
            }
        }

        private void OnPlayerNameResponse(long playerId, PlayerNameResponse response)
        {
            _logger.LogInformation("Player name received. PlayerId={playerId}, Name={name}", playerId, response?.Name);
            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(playerId);
                if (existingPlayer == null)
                {
                    _logger.LogWarning("Can't process player name update because player doesn't exist. PlayerId={playerId}, Name={name}", playerId, response?.Name);
                    return;
                }

                if (string.IsNullOrEmpty(response.Name))
                {
                    _logger.LogWarning("Can't process player name update because player name is missing. PlayerId={playerId}, Name={name}", playerId, response?.Name);
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
                    _logger.LogWarning("Player already exists. PlayerId={playerId}", playerId);
                    return;
                }

                var player = new NetworkPlayer(playerId);
                _playersList.Add(player);
                _logger.LogWarning("Sending player name request. PlayerId={playerId}", playerId);
                _networkServer.Send(playerId, new PlayerNameRequest());
            }
        }

        private void OnPlayerDisconnected(long playerId)
        {
            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(playerId);
                if (existingPlayer == null)
                {
                    _logger.LogWarning("Nothing to cleanup since player doesn't exist. PlayerId={playerId}", playerId);
                    return;
                }

                _playersList.Remove(existingPlayer);
                if (!string.IsNullOrEmpty(existingPlayer.Name))
                {
                    _lobbyWindowController.UpdatePlayers(_playersList);
                }

                // send updates to other clients
            }
        }

        private void OnServerStarted(EndPoint point)
        {
            _playersList.Add(new NetworkPlayer(0)
            {
                Name = Guid.NewGuid().ToString().Split('-').First()
            });

            _lobbyWindowController.UpdatePlayers(_playersList);
            _lobbyWindowController.UpdateServerInfo(point);
        }

        private NetworkPlayer GetPlayer(long playerId)
        {
            return _playersList.FirstOrDefault(p => p.Id == playerId);
        }
    }
}
