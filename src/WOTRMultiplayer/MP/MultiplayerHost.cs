using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using Kingmaker.EntitySystem.Persistence;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerHost : IMultiplayerHost
    {
        private readonly ILogger<MultiplayerHost> _logger;
        private readonly INetworkServer _networkServer;
        private readonly IFileSystemService _fileSystemService;
        private readonly IMultiplayerSettingsProvider _multiplayerSettingsProvider;
        private readonly IGameInteractionService _gameInteractionService;

        private NetworkGameStage Status => _game?.Stage ?? NetworkGameStage.None;

        private readonly object _actionlock = new();
        public const int LocalHostPlayerId = -1;
        private NetworkGame _game;

        public Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }
        public Action<EndPoint> OnConnected { get; set; }
        public Action<SaveInfo> OnStartGame { get; set; }

        public bool IsActive => _networkServer.IsActive;

        public bool IsInLobby => IsActive && Status == NetworkGameStage.Lobby;

        public NetworkGame CurrentGame => _game;

        public MultiplayerHost(
            ILogger<MultiplayerHost> logger,
            IGameInteractionService gameInteractionService,
            IMultiplayerSettingsProvider multiplayerSettingsProvider,
            IFileSystemService fileSystemService,
            INetworkServer networkServer)
        {
            _logger = logger;
            _networkServer = networkServer;
            _fileSystemService = fileSystemService;
            _multiplayerSettingsProvider = multiplayerSettingsProvider;
            _gameInteractionService = gameInteractionService;
        }

        public void Create(SaveInfo save, List<NetworkCharacter> characters)
        {
            if (_networkServer.IsActive)
            {
                _networkServer.Dispose();
            }

            RegisterHandlers();

            _game?.Reset();

            _game = new NetworkGame(save);
            _game.Characters.AddRange(characters);

            _networkServer.Start(_multiplayerSettingsProvider.Settings.HostPortRangeStart, _multiplayerSettingsProvider.Settings.HostPortRangeEnd);

            _logger.LogInformation("Host has been created. SavePath={savePath}, Portraits={portraits}", _game.Save.FolderName, string.Join(";", _game.Characters.Select(c => c.Portrait)));
        }

        public void UpdateSaveGame(SaveInfo save, List<NetworkCharacter> characters)
        {
            _game.Save = save;
            _game.Characters.Clear();
            _game.Characters.AddRange(characters);
            var host = GetHost();
            foreach (var character in characters)
            {
                character.Owner = host;
            }

            _logger.LogInformation("Notifying game characters changed. Portraits={portraits}", string.Join(";", _game.Characters.Select(c => c.Portrait)));
            var message = CreateNotifyGameCharactersChanged();
            _networkServer.SendAll(message);
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

                if (_game.Characters.Count < characterIndex)
                {
                    _logger.LogError("Unable to change character owner as characterIndex is out of range. CharacterOwnersCount={characterOwnersCount}, CharacterIndex={characterIndex}", _game.Characters.Count, characterIndex);
                    return;
                }

                var character = _game.Characters[characterIndex];
                character.Owner = player;
                _logger.LogInformation("New character owner. CharacterName={characterName}, PlayerId={playerId}, PlayerName={playerName}", character.Name, player.Id, player.Name);

                var charactersOwnerChanged = CreateNotifyCharactersOwnerChanged();
                _networkServer.SendAll(charactersOwnerChanged);
            }
        }

        public void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation)
        {
            _logger.LogInformation("Moving character. Name={characterName}, Destination={destination}", characterName, destination);
            var message = new NotifyCharacterMove
            {
                CharacterName = characterName,
                DestinationX = destination.X,
                DestinationY = destination.Y,
                DestinationZ = destination.Z,
                Delay = delay,
                Orientation = orientation
            };
            _networkServer.SendAll(message);
        }

        public void Dispose()
        {
            _logger.LogInformation("Dispose");

            lock (_actionlock)
            {
                _game?.Reset();
            }

            _networkServer.Dispose();
        }

        public bool CanControlCharacter(string characterName)
        {
            if (_game == null)
            {
                return false;
            }

            var character = _game.Characters.FirstOrDefault(c => c.Name.Contains(characterName)); // should be a strict match later on
            if (character == null)
            {
                _logger.LogWarning("Unable to find character in the list. CharacterName={characterName}", characterName);
                return false;
            }

            return character.Owner != null && character.Owner.Id == LocalHostPlayerId;
        }

        public bool ReadyChanged()
        {
            var player = _game.Players.First(p => p.Id == LocalHostPlayerId); // host should be always present
            var readyChanged = new PlayerReadyStatusChanged { PlayerId = LocalHostPlayerId, IsReady = !player.IsReady };
            OnPlayerReadyStatusChanged(player.Id, readyChanged);
            return readyChanged.IsReady;
        }

        public void Start()
        {
            _logger.LogInformation("Starting game...");
            // it should be fine to block current thread
            var content = _fileSystemService.GetFile(_game.Save.FolderName);
            if (content == null)
            {
                _logger.LogError("Unable to start a game due to missing save file. Path={savePath}", _game.Save.FolderName);
                return;
            }

            _game.Stage = NetworkGameStage.Initializing;
            var gameStageChanged = new NotifyGameStageChanged { Stage = _game.Stage.ToString() };
            _networkServer.SendAll(gameStageChanged);

            lock (_actionlock)
            {
                var saveGameMessageAssigned = new NotifySaveGameAssigned { Content = content };
                _logger.LogInformation($"Sending save game file content to all players. Size={saveGameMessageAssigned.Content.Length}");
                _networkServer.SendAll(saveGameMessageAssigned);
                _game.Stage = NetworkGameStage.WaitingForPlayersInitialization;
                _logger.LogInformation("Waiting for players to confirm delivery. GameStatus={gameStatus}", _game.Stage);
                GetHost().IsSyncedToStartGame = true;
            }

            TryStartGame();
        }

        public void GameLoaded()
        {
            _logger.LogInformation("Game loaded, pausing...");
            _gameInteractionService.Pause(true);

            var host = GetHost();
            host.IsLoading = false;

            TryUnpauseGame();
        }

        public void Pause()
        {
            _logger.LogInformation("Sending pausing notification");
            var message = new NotifyGamePauseChanged { IsPaused = true };
            _networkServer.SendAll(message);
        }

        public void Unpause()
        {
            _logger.LogInformation("Sending unpausing notification");
            var message = new NotifyGamePauseChanged { IsPaused = false };
            _networkServer.SendAll(message);
        }

        private void TryUnpauseGame()
        {
            var canUnpause = false;

            lock (_actionlock)
            {
                canUnpause = _game.Players.All(p => !p.IsLoading);
            }

            if (canUnpause)
            {
                _logger.LogInformation("Unpausing game");
                _gameInteractionService.Pause(false);
            }
        }

        private void OnGameLoaded(long playerId, GameLoaded loaded)
        {
            _logger.LogInformation("OnGameLoaded. PlayerId={playerId}", playerId);
            lock (_actionlock)
            {
                var player = GetPlayer(playerId);
                if (player == null)
                {
                    _logger.LogError("Can't set loading status for missing player. PlayerId={playerId}", playerId);
                    return;
                }

                player.IsLoading = false;
            }

            TryUnpauseGame();
        }

        private void TryStartGame()
        {
            var canStart = false;

            lock (_actionlock)
            {
                canStart = _game.Players.All(p => p.IsSyncedToStartGame);
            }

            if (canStart)
            {
                _logger.LogInformation("Starting game");
                foreach (var player in _game.Players)
                {
                    player.IsLoading = true;
                }

                _networkServer.SendAll(new NotifyGameStarted());
                OnStartGame?.Invoke(_game.Save);
            }
        }

        private NotifyCharactersOwnerChanged CreateNotifyCharactersOwnerChanged()
        {
            _game.Characters.Select((character, index) => new Networking.Messages.NetworkCharacterOwner { CharacterIndex = index, PlayerId = character.Owner.Id });
            var charactersOwnerChanged = new NotifyCharactersOwnerChanged
            {
                Owners = [.. _game.Characters.Select((character, index) => new Networking.Messages.NetworkCharacterOwner { CharacterIndex = index, PlayerId = character.Owner.Id })]
            };

            return charactersOwnerChanged;
        }

        private NetworkPlayer GetHost()
        {
            return _game.Players.First(f => f.Id == LocalHostPlayerId);
        }

        private void RegisterHandlers()
        {
            _networkServer.OnClientConnected = OnPlayerConnected;
            _networkServer.OnClientDisconnected = OnPlayerDisconnected;
            _networkServer.OnServerStarted = OnServerStarted;

            _networkServer
                .Register<PlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
                .Register<PlayerNameResponse>(OnPlayerNameResponse)
                .Register<PlayerSaveGameSyncChanged>(OnPlayerSaveGameSyncChanged)
                .Register<CharacterMove>(OnCharacterMove)
                .Register<GameLoaded>(OnGameLoaded)
                .Register<GamePauseChanged>(OnGamePauseChanged)
                ;
        }

        private void OnGamePauseChanged(long playerId, GamePauseChanged pauseChanged)
        {
            _logger.LogInformation("Received GamePauseChanged. PlayerId={playerId}, IsPaused={isPaused}", playerId, pauseChanged.IsPaused);
            var message = new NotifyGamePauseChanged { IsPaused = pauseChanged.IsPaused };
            _networkServer.SendAllExcept(playerId, message);
            _gameInteractionService.Pause(pauseChanged.IsPaused);
        }

        private void OnCharacterMove(long playerId, CharacterMove move)
        {
            _logger.LogInformation("Received CharacterMove. PlayerId={playerId}, CharacterName={characterName}, DestinationX={x}, DestinationY={y}, DestinationZ={z}", playerId, move.CharacterName, move.DestinationX, move.DestinationY, move.DestinationZ);

            var destination = new Vector3(move.DestinationX, move.DestinationY, move.DestinationZ);
            _gameInteractionService.MoveCharacter(move.CharacterName, destination, move.Delay, move.Orientation);

            var notifyMove = new NotifyCharacterMove
            {
                CharacterName = move.CharacterName,
                DestinationX = move.DestinationX,
                DestinationY = move.DestinationY,
                DestinationZ = move.DestinationZ,
                Delay = move.Delay,
                Orientation = move.Orientation
            };
            _networkServer.SendAllExcept(playerId, notifyMove);
        }

        private void OnPlayerSaveGameSyncChanged(long playerId, PlayerSaveGameSyncChanged changed)
        {
            _logger.LogInformation("Received PlayerSaveGameSyncChanged. PlayerId={playerId}, SyncStatus={syncStatus}", playerId, changed.IsSynced);
            lock (_actionlock)
            {
                var player = GetPlayer(playerId);
                if (player == null)
                {
                    _logger.LogError("Player is missing. Game won't start. Player Id={playerId}", playerId);
                    return;
                }

                player.IsSyncedToStartGame = changed.IsSynced;
            }

            TryStartGame();
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

                    _logger.LogInformation("Sending CharactersOwnerChanged to new player. PlayerId={playerId}", playerId);
                    var charactersOwnerChanged = CreateNotifyCharactersOwnerChanged();
                    _networkServer.Send(playerId, charactersOwnerChanged);
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
                _logger.LogInformation("Sending player name request. PlayerId={playerId}", playerId);
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

                // TBD send updates to other clients
            }
        }

        private void OnServerStarted(EndPoint endpoint)
        {
            var hostPlayer = new NetworkPlayer(LocalHostPlayerId)
            {
                Name = _multiplayerSettingsProvider.Settings.PlayerName
            };

            _game.Players.Add(hostPlayer);
            _game.Endpoint = endpoint;

            foreach (var character in _game.Characters)
            {
                character.Owner = hostPlayer;
            }

            OnConnected?.Invoke(endpoint);
            OnPlayersChanged?.Invoke(_game.Players);
        }

        private NetworkPlayer GetPlayer(long playerId)
        {
            return _game.Players.FirstOrDefault(p => p.Id == playerId);
        }

        private NotifyGameCharactersChanged CreateNotifyGameCharactersChanged()
        {
            var message = new NotifyGameCharactersChanged
            {
                Characters = [.. _game.Characters.Select(c => new Networking.Messages.NetworkCharacter { Name = c.Name, Portrait = c.Portrait })]
            };
            return message;
        }

        public void LeaveArea(string areaExitId)
        {
            _logger.LogInformation("Sending NotifyPartyLeaveArea. AreaExitId={areaExitId}", areaExitId);
            var message = new NotifyPartyLeaveArea { AreaExitId = areaExitId };
            _networkServer.SendAll(message);
        }
    }
}
