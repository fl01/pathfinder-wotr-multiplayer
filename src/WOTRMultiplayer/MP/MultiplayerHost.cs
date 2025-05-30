using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Lobby;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerHost : IMultiplayerHost
    {
        private readonly ILogger<MultiplayerHost> _logger;
        private readonly INetworkServer _networkServer;
        private NetworkGameStatus Status => _game?.Status ?? NetworkGameStatus.None;

        private readonly object _actionlock = new();
        public const int LocalHostPlayerId = -1;
        private NetworkGame _game;

        public Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }
        public Action<EndPoint> OnConnected { get; set; }

        public bool IsActive => _networkServer.IsActive;

        public bool IsInLobby => IsActive && Status == NetworkGameStatus.Lobby;

        public MultiplayerHost(
            ILogger<MultiplayerHost> logger,
            INetworkServer networkServer)
        {
            _logger = logger;
            _networkServer = networkServer;
        }

        public void Create(string savePath, List<string> portraits, MultiplayerSettings settings)
        {
            if (_networkServer.IsActive)
            {
                _networkServer.Dispose();
            }

            RegisterHandlers();

            _game?.Reset();

            _game = new NetworkGame(savePath);
            _game.Portraits.AddRange(portraits);

            _networkServer.Start();

            _logger.LogInformation("Host has been created. SavePath={savePath}, Portraits={portraits}", _game.SavePath, string.Join(";", _game.Portraits));
        }

        public void Dispose()
        {
            _logger.LogInformation("Dispose");

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

        public void NotifyGameCharactersChanged(string savePath, List<string> portraits)
        {
            _game.Portraits.Clear();
            _game.Portraits.AddRange(portraits);
            _game.SavePath = savePath;

            _logger.LogInformation("Notifying game characters changed. Portraits={portraits}", string.Join(";", _game.Portraits));
            var message = CreateNotifyGameCharactersChanged();
            _networkServer.SendAll(message);
        }

        public void Start()
        {
            _logger.LogInformation("Starting game...");
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

                OnPlayersChanged?.Invoke(_game.Players);
                _logger.LogInformation("Sending ready status changed. PlayerId={playerId}, IsReady={isReady}", playerId, existingPlayer.IsReady);
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

                OnPlayersChanged?.Invoke(_game.Players);

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
                    OnPlayersChanged?.Invoke(_game.Players);
                }

                // send updates to other clients
            }
        }

        private void OnServerStarted(EndPoint endpoint)
        {
            _game.Players.Add(new NetworkPlayer(LocalHostPlayerId)
            {
                Name = Guid.NewGuid().ToString().Split('-').First()
            });

            OnConnected?.Invoke(endpoint);
            OnPlayersChanged?.Invoke(_game.Players);
        }

        private NetworkPlayer GetPlayer(long playerId)
        {
            return _game.Players.FirstOrDefault(p => p.Id == playerId);
        }

        private NotifyGameCharactersChanged CreateNotifyGameCharactersChanged()
        {
            var message = new NotifyGameCharactersChanged { Portraits = _game.Portraits };
            return message;
        }
    }
}
