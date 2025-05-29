using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages._100_Lobby;
using WOTRMultiplayer.Networking.Messages.Lobby;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerHost : IMultiplayerHost
    {
        private readonly ILogger<MultiplayerHost> _logger;
        private readonly INetworkServer _networkServer;
        private readonly ILobbyWindowController _lobbyWindowController;

        private readonly object _actionlock = new();
        public const int LocalHostPlayerId = -1;
        private NetworkGame _game;

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

        public void Start(string gameName, List<string> portraits, MultiplayerSettings settings)
        {
            if (_networkServer.IsActive)
            {
                _networkServer.Dispose();
            }

            RegisterHandlers();

            _game?.Players.Clear();
            _game?.Portraits.Clear();

            _game = new NetworkGame(gameName);
            _game.Portraits.AddRange(portraits);

            _networkServer.Start();
        }

        public void Stop()
        {
            _logger.LogInformation("Stop");

            lock (_actionlock)
            {
                _game?.Players.Clear();
            }

            _networkServer.Dispose();
        }

        public bool ReadyChanged()
        {
            var player = _game.Players.First(p => p.Id == LocalHostPlayerId); // host should be always present
            var readyChanged = new PlayerReadyStatusChanged { PlayerId = LocalHostPlayerId, IsReady = !player.IsReady };
            OnPlayerReadyStatusChanged(player.Id, readyChanged);
            return readyChanged.IsReady;
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
                    _logger.LogWarning("Can't find existing player. PlayerId={playerId}", playerId);
                    return;
                }

                existingPlayer.IsReady = readyStatusChanged.IsReady;

                UpdatePlayersUI();
                _logger.LogWarning("Sending ready status changed. PlayerId={playerId}, IsReady={isReady}", playerId, existingPlayer.IsReady);
                _networkServer.SendAll(readyStatusChanged);
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

                UpdatePlayersUI();

                var players = _game.Players.Select(x => new Networking.Messages.NetworkPlayer { Id = x.Id, Name = x.Name, IsReady = x.IsReady }).ToList();
                var playersChanged = new NotifyPlayersChanged { Players = players };
                _networkServer.SendAll(playersChanged);

                var notifyGameCharactersChanged = CreateNotifyGameCharactersChanged();
                _networkServer.Send(playerId, notifyGameCharactersChanged);
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
                _game.Players.Add(player);
                _logger.LogWarning("Sending player name request. PlayerId={playerId}", playerId);
                _networkServer.Send(playerId, new PlayerNameRequest { PlayerId = playerId });
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

                _game.Players.Remove(existingPlayer);
                if (!string.IsNullOrEmpty(existingPlayer.Name))
                {
                    UpdatePlayersUI();
                }

                // send updates to other clients
            }
        }

        private void OnServerStarted(EndPoint point)
        {
            _game.Players.Add(new NetworkPlayer(LocalHostPlayerId)
            {
                Name = Guid.NewGuid().ToString().Split('-').First()
            });

            UpdateServerUI(point);
            UpdatePlayersUI();
        }

        private NetworkPlayer GetPlayer(long playerId)
        {
            return _game.Players.FirstOrDefault(p => p.Id == playerId);
        }

        public void NotifyGameCharactersChanged(string gameName, List<string> portraits)
        {
            _game.Name = gameName;
            _game.Portraits.Clear();
            _game.Portraits.AddRange(portraits);

            _logger.LogInformation("Notifying game characters changed. Name={gameName}, Portraits={portraits}", gameName, string.Join(";", portraits));
            var message = CreateNotifyGameCharactersChanged();
            _networkServer.SendAll(message);
        }

        private NotifyGameCharactersChanged CreateNotifyGameCharactersChanged()
        {
            var message = new NotifyGameCharactersChanged { GameName = _game.Name, Portraits = _game.Portraits };
            return message;
        }

        private void UpdatePlayersUI()
        {
            _lobbyWindowController.UpdatePlayers(_game.Players);
        }

        private void UpdateServerUI(EndPoint endpoint)
        {
            _lobbyWindowController.UpdateServerInfo(endpoint.ToString());
        }
    }
}
