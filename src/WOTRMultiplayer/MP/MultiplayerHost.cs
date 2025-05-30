using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.IO;
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
        private readonly IFileSystemService _fileSystemService;

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
            IFileSystemService fileSystemService,
            INetworkServer networkServer)
        {
            _logger = logger;
            _networkServer = networkServer;
            _fileSystemService = fileSystemService;
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
            _game.CharacterOwners = [.. Enumerable.Range(0, Main.MaxCharacters).Select(x => new NetworkCharacterOwner { CharacterIndex = x, PlayerId = LocalHostPlayerId })];

            _networkServer.Start();

            _logger.LogInformation("Host has been created. SavePath={savePath}, Portraits={portraits}", _game.SavePath, string.Join(";", _game.Portraits));
        }

        public void ChangeCharacterOwner(int characterIndex, int playerIndex)
        {
            lock (_actionlock)
            {
                if (_game.Players.Count < playerIndex)
                {
                    _logger.LogError("Unable to change character owner as playerIndex is out of range. PlayersCount={playersCount}, PlayerIndex={playerIndex}", _game.Players.Count, playerIndex);
                    return;
                }

                var player = _game.Players[playerIndex];

                if (_game.CharacterOwners.Count < characterIndex)
                {
                    _logger.LogError("Unable to change character owner as characterIndex is out of range. CharacterOwnersCount={characterOwnersCount}, CharacterIndex={characterIndex}", _game.CharacterOwners.Count, characterIndex);
                    return;
                }

                var owner = _game.CharacterOwners[characterIndex];
                owner.CharacterIndex = characterIndex;
                owner.PlayerId = player.Id;

                var charactersOwnerChanged = new NotifyCharactersOwnerChanged
                {
                    Owners = [.. _game.CharacterOwners.Select(o => new Networking.Messages.NetworkCharacterOwner { CharacterIndex = o.CharacterIndex, PlayerId = o.PlayerId })]
                };
                _networkServer.SendAll(charactersOwnerChanged);
            }
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
            // it should be fine to block current thread
            var content = _fileSystemService.GetFile(_game.SavePath);
            if (content == null)
            {
                _logger.LogError("Unable to start a game due to missing save file. Path={savePath}", _game.SavePath);
                return;
            }

            _game.Status = NetworkGameStatus.Initializing;
            var gameStatusChanged = new NotifyGameStatusChanged { Status = _game.Status.ToString() };
            _networkServer.SendAll(gameStatusChanged);

            var saveGameMessageAssigned = new NotifySaveGameAssigned { Content = content };
            _logger.LogInformation($"Sending save game file content to all players. Size={saveGameMessageAssigned.Content.Length}");
            _networkServer.SendAll(saveGameMessageAssigned);
            _game.Status = NetworkGameStatus.WaitingForPlayersInitialization;
            _logger.LogInformation("Waiting for players to confirm delivery. GameStatus={gameStatus}", _game.Status);
            // wait for all players to receive save -> Initialized -> send load game command to clients -> load game for a host
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
            try
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
                    _logger.LogInformation("Sending players changed to ALL players");
                    _networkServer.SendAll(playersChanged);

                    var notifyGameCharactersChanged = CreateNotifyGameCharactersChanged();
                    _logger.LogInformation("Sending GameCharactersChanged to new player. PlayerId={playerId}", playerId);
                    _networkServer.Send(playerId, notifyGameCharactersChanged);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle player name response");
                throw;
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
