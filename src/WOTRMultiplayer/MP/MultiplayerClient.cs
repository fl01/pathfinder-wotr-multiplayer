using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.RegularExpressions;
using Kingmaker.EntitySystem.Persistence;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.Saves;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerClient : IMultiplayerClient
    {
        private readonly ILogger<MultiplayerClient> _logger;
        private readonly IIPEndPointParser _ipEndPointParser;
        private readonly IFileSystemService _fileSystemService;
        private readonly ISaveGameService _saveGameService;
        private readonly INetworkServerClient _networkServerClient;
        private readonly IMultiplayerSettingsProvider _multiplayerSettingsProvider;
        private readonly IGameInteractionService _gameInteractionService;
        public const int LocalHostPlayerId = -1;

        private NetworkGame _game;
        private readonly object _actionlock = new();
        private long _localPlayerId;

        public Action<string> OnNetworkError { get; set; }

        public Action<EndPoint> OnConnected { get; set; }

        public Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }
        public Action<List<NetworkCharacter>> OnGameCharactersChanged { get; set; }
        public Action<int, int> OnCharacterOwnerChanged { get; set; }
        public Action<SaveInfo> OnStartGame { get; set; }

        public Action OnDisconnected { get; set; }

        public bool IsActive => _networkServerClient.IsActive;

        public bool IsConnecting => _networkServerClient.IsConnecting;

        private NetworkGameStage Status => _game?.Stage ?? NetworkGameStage.None;

        public bool IsInLobby => IsActive && Status == NetworkGameStage.Lobby;

        public NetworkGame CurrentGame => _game;

        public MultiplayerClient(
            ILogger<MultiplayerClient> logger,
            IGameInteractionService gameInteractionService,
            IIPEndPointParser ipEndPointParser,
            IMultiplayerSettingsProvider multiplayerSettingsProvider,
            ISaveGameService saveGameService,
            IFileSystemService fileSystemService,
            INetworkServerClient networkServerClient)
        {
            _logger = logger;
            _ipEndPointParser = ipEndPointParser;
            _fileSystemService = fileSystemService;
            _saveGameService = saveGameService;
            _networkServerClient = networkServerClient;
            _multiplayerSettingsProvider = multiplayerSettingsProvider;
            _gameInteractionService = gameInteractionService;
        }

        public ConnectLobbyResult Connect(string address)
        {
            if (_networkServerClient.IsActive)
            {
                Dispose();
            }

            if (!_ipEndPointParser.TryParse(address, out IPEndPoint endpoint))
            {
                return ConnectLobbyResult.Error(UIStringConsts.MultiplayerClient.Errors.InvalidIP);
            }

            if (endpoint.Port <= 0 || endpoint.Port > ushort.MaxValue)
            {
                return ConnectLobbyResult.Error(UIStringConsts.MultiplayerClient.Errors.InvalidPort);
            }

            RegisterHandlers();

            _networkServerClient.ConnectAsync(endpoint.Address.ToString(), endpoint.Port);

            return ConnectLobbyResult.Ok();
        }

        public bool ReadyChanged()
        {
            _logger.LogInformation("Ready changed");
            var player = _game.Players.First(p => p.Id == _localPlayerId); // local client should be always present
            player.IsReady = !player.IsReady;
            var readyChanged = new PlayerReadyStatusChanged { PlayerId = player.Id, IsReady = player.IsReady };
            _networkServerClient.SendAsync(readyChanged).Wait();
            return readyChanged.IsReady;
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing");

            _game?.Reset();

            _networkServerClient?.Dispose();
        }

        private void RegisterHandlers()
        {
            _networkServerClient
                .Register<PlayerNameRequest>(OnPlayerNameRequest)
                .Register<PlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
                .Register<NotifyPlayersChanged>(OnNotifyPlayersChanged)
                .Register<NotifyGameCharactersChanged>(OnNotifyGameCharactersChanged)
                .Register<NotifySaveGameAssigned>(OnNotifySaveGameAssigned)
                .Register<NotifyGameStageChanged>(OnNotifyGameStageChanged)
                .Register<NotifyCharactersOwnerChanged>(OnNotifyCharactersOwnerChanged)
                .Register<NotifyGameStarted>(OnNotifyGameStarted)
                .Register<NotifyCharacterMove>(OnNotifyCharacterMove)
                ;

            _networkServerClient.OnError = OnNetworkClientError;
            _networkServerClient.OnConnected = OnNetworkClientConnected;
        }

        private void OnNotifyCharacterMove(NotifyCharacterMove move)
        {
            _logger.LogInformation("Received NotifyCharacterMove. PlayerId={playerId}, CharacterName={characterName}, DestinationX={x}, DestinationY={y}, DestinationZ={z}", move.CharacterName, move.DestinationX, move.DestinationY, move.DestinationZ);

            var destination = new Vector3(move.DestinationX, move.DestinationY, move.DestinationZ);
            _gameInteractionService.MoveCharacter(move.CharacterName, destination, move.Delay, move.Orientation);
        }

        private void OnNotifyGameStarted(NotifyGameStarted started)
        {
            _logger.LogInformation("NotifyGameStarted");
            OnStartGame?.Invoke(_game.Save);
        }

        private void OnNotifyCharactersOwnerChanged(NotifyCharactersOwnerChanged changed)
        {
            _logger.LogInformation("NotifyCharactersOwnerChanged");
            try
            {
                for (int i = 0; i < changed.Owners.Count; i++)
                {
                    var owner = changed.Owners[i];
                    var player = _game.Players.FirstOrDefault(p => p.Id == owner.PlayerId);
                    if (player == null)
                    {
                        _logger.LogWarning("Unable to assign character ownership for missing player. PlayerId={playerId}", owner.PlayerId);
                        player = _game.Players.First();
                    }

                    _game.Characters[owner.CharacterIndex].Owner = player;
                    OnCharacterOwnerChanged?.Invoke(owner.CharacterIndex, _game.Players.IndexOf(player));

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle {messageType}", nameof(NotifyCharactersOwnerChanged));
                throw;
            }
        }

        private void OnNotifyGameStageChanged(NotifyGameStageChanged changed)
        {
            _logger.LogInformation("Received NotifyGameStatusChanged. Status={newGameStatus}", changed.Stage);
            _game.Stage = (NetworkGameStage)Enum.Parse(typeof(NetworkGameStage), changed.Stage, true);
        }

        private void OnNotifySaveGameAssigned(NotifySaveGameAssigned assigned)
        {
            _logger.LogInformation("Received save game file content. GameStatus={status} Size={contentSize}", _game.Stage, assigned.Content.Length);

            var baseUnityPath = _saveGameService.GetSaveGamePath();
            var multiplayerPath = Regex.Replace(baseUnityPath, "(((\\\\|\\/)+)(Saved Games)((\\\\|\\/)+))$", "/Saved Multiplayer Games/");
            var savePath = Path.Combine(multiplayerPath, "latest save.zks");
            _logger.LogInformation("Save game path changed. Path={path}", savePath);
            if (!_fileSystemService.WriteFile(savePath, assigned.Content))
            {
                _logger.LogError("Unable to store save game");
                // on error?
                return;
            }

            _game.Save = _saveGameService.LoadSave(savePath);
            _logger.LogInformation("Game is ready to be started. SaveName={saveName}, Area={saveArea}", _game.Save?.Name, _game.Save?.Area.AreaDisplayName);
            _networkServerClient.SendAsync(new PlayerSaveGameSyncChanged { IsSynced = true }).Wait();
        }

        private void OnPlayerReadyStatusChanged(PlayerReadyStatusChanged readyStatusChanged)
        {
            _logger.LogInformation("Player ready status changed received. PlayerId={playerId}, IsReady={isReady}", readyStatusChanged.PlayerId, readyStatusChanged.IsReady);

            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(readyStatusChanged.PlayerId);
                if (existingPlayer == null)
                {
                    _logger.LogWarning("Can't find existing player. PlayerId={playerId}", readyStatusChanged.PlayerId);
                    return;
                }

                existingPlayer.IsReady = readyStatusChanged.IsReady;
                OnPlayersChanged?.Invoke(_game.Players);
            }
        }

        private void OnNotifyGameCharactersChanged(NotifyGameCharactersChanged changed)
        {
            _logger.LogInformation("{messageType} received. Portraits={portraits}", nameof(NotifyGameCharactersChanged), string.Join(";", changed.Characters.Select(c => c.Portrait)));
            _game.Characters.Clear();
            _game.Characters.AddRange(changed.Characters.Select(c => new NetworkCharacter { Name = c.Name, Portrait = c.Portrait }));
            OnGameCharactersChanged?.Invoke(_game.Characters);
        }

        private void OnNotifyPlayersChanged(NotifyPlayersChanged changed)
        {
            _logger.LogInformation("{messageType} received. PlayersCount={playersCount}}", nameof(NotifyPlayersChanged), changed.Players.Count);
            _game.Players.Clear();
            var players = changed.Players.Select(p => new NetworkPlayer(p.Id) { IsReady = p.IsReady, Name = p.Name }).ToList();
            _game.Players.AddRange(players);

            // add or remove players should cause owner reset
            foreach (var character in _game.Characters)
            {
                character.Owner = _game.Players.First();
            }

            OnPlayersChanged?.Invoke(_game.Players);
        }

        private void OnNetworkClientConnected(EndPoint endpoint)
        {
            _game = new NetworkGame(null)
            {
                Endpoint = endpoint
            };
            OnConnected?.Invoke(endpoint);
        }

        private void OnNetworkClientError(Exception exception)
        {
            if (exception is SocketException socketException)
            {
                string error = string.Empty;
                switch (socketException.SocketErrorCode)
                {
                    case SocketError.OperationAborted: // client disconnected by a user
                        _logger.LogWarning("Skipping notification. SocketCode={socketCode}", socketException.SocketErrorCode);
                        break;
                    case SocketError.ConnectionReset:
                        error = "You have been disconnected.";
                        break;
                    default:
                        error = $"Network error occurred. Error code: {socketException.SocketErrorCode}";
                        break;
                }

                if (!string.IsNullOrEmpty(error))
                {
                    OnNetworkError?.Invoke(error);
                }

                return;
            }

            // should never happen?
            _logger.LogError(exception, "Generic network error occurred");
            OnNetworkError?.Invoke("Generic network error occurred.");
        }

        private void OnPlayerNameRequest(PlayerNameRequest request)
        {
            _logger.LogInformation("Player name requested. PlayerId={playerId}", request.PlayerId);
            _localPlayerId = request.PlayerId;

            var nameResponse = new PlayerNameResponse() { Name = _multiplayerSettingsProvider.Settings.PlayerName };
            _networkServerClient.SendAsync(nameResponse).Wait();
            _logger.LogInformation("Player name has been sent. Name={name}", nameResponse.Name);
        }

        private NetworkPlayer GetPlayer(long playerId)
        {
            return _game.Players.FirstOrDefault(p => p.Id == playerId);
        }

        public void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation)
        {
            _logger.LogInformation("Sending CharacterMove. Name={characterName}, Destination={destination}", characterName, destination);
            var message = new CharacterMove
            {
                CharacterName = characterName,
                DestinationX = destination.X,
                DestinationY = destination.Y,
                DestinationZ = destination.Z,
                Delay = delay,
                Orientation = orientation
            };
            _networkServerClient.SendAsync(message).Wait();
        }
    }
}
