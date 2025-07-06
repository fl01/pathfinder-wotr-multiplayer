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
using WOTRMultiplayer.MP.Entities.Rolls;
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

            return character.Owner != null && character.Owner.Id == _localPlayerId;
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

        public void GameLoaded()
        {
            _logger.LogInformation("Game loaded");

            // assumption: should be done after each area load aswell
            SoftReset();

            PartyChanged();

            _gameInteractionService.Pause(true);

            _networkServerClient.SendAsync(new GameLoaded()).Wait();
        }

        /// <summary>
        /// Reloads current party characters and tries to merge ownership
        /// </summary>
        public void PartyChanged()
        {
            _logger.LogInformation("Updating current characters & merging ownership");

            // could be synced from host, but state is the same anyway
            var partyCharacters = _gameInteractionService.GetPartyPlayers();
            if (partyCharacters.Count == 0)
            {
                return;
            }

            var oldCharacters = _game.Characters.ToList();
            _game.Characters = [.. partyCharacters];
            var defaultOwner = GetPlayer(LocalHostPlayerId);
            foreach (var character in _game.Characters)
            {
                var existingOwnershipConfiguration = oldCharacters.FirstOrDefault(old =>
                    old.Name == character.Name || old.Name.Contains(character.Name));
                if (existingOwnershipConfiguration?.Owner != null)
                {
                    character.Owner = existingOwnershipConfiguration.Owner;
                    _logger.LogInformation("Character ownership has been preserved. CharacterName={characterName}, Owner={ownerId}", character.Name, character.Owner.Id);
                    continue;
                }

                character.Owner = defaultOwner;
                _logger.LogInformation("Character ownership has been assigned to default player (host). CharacterName={characterName}, Owner={ownerId}", character.Name, character.Owner.Id);
            }
        }

        public void Pause()
        {
            //_logger.LogInformation("Sending pausing notification");

            //var message = new GamePauseChanged { IsPaused = true };
            //_networkServerClient.SendAsync(message).Wait();
        }

        public void Unpause()
        {
            //_logger.LogInformation("Sending unpausing notification");
            //var message = new GamePauseChanged { IsPaused = false };
            //_networkServerClient.SendAsync(message).Wait();
        }

        public NetworkDiceRoll GetRoll(int rollId)
        {
            _logger.LogInformation("Retrieving roll from the host. RollId={rollId}, RollResult={rollResult}", rollId);

            var request = new RollRequest { RollId = rollId };
            // it's important to block current thread since we cannot proceed without response
            // yeah most likely it will cause the game to freeze in case of bad network
            var response = _networkServerClient.SendAndWaitForAsync<RollResponse>(request).Result;

            if (response == null)
            {
                _logger.LogError("Unable to retrieve roll from host. RollId={rollId}", rollId);
                return null;
            }

            if (response.Roll == null)
            {
                _logger.LogError("Host returned null roll. RollId={rollId}", rollId);
                return null;
            }

            _logger.LogInformation("Roll has been retrieved from the host. RollId={rollId}, RollResult={rollResult}", rollId, response.Roll.Result);

            return new NetworkDiceRoll
            {
                Result = response.Roll.Result,
                RollHistory = [.. response.Roll.RollHistory]
            };
        }

        public void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer)
        {
            _logger.LogInformation("Showing dialog Cue. DialogName={dialogName}, CueName={cueName}, HasSystemAnswer={hasSystemAnswer}", dialogName, cueName, hasSystemAnswer);
            if (hasSystemAnswer)
            {
                _gameInteractionService.SetDialogContinueButtonState(false);
            }

            if (_game.Dialog != null && _game.Dialog.Name != dialogName)
            {
                _logger.LogWarning("Previous dialog has not been disposed correctly. PreviousDialogName={previousDialogName}, CurrentDialogName={currentDialogName}", _game.Dialog.Name, dialogName);
                _game.Dialog = null;
            }

            _game.Dialog ??= new NetworkDialog(dialogName);
            _game.Dialog.CurrentCueName = cueName;
            _game.Dialog.Answer = null;

            _gameInteractionService.MarkSuggestedDialogAnswers([]);

            var message = new CueWitnessed { CueName = cueName, DialogName = dialogName };
            _networkServerClient.SendAsync(message).Wait();
        }

        public bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId)
        {
            _logger.LogInformation("Select Dialog Answer. DialogName={dialogName}, CueName={cueName}, Answer={answer}, IsExitAnswer={isExitAnswer}, ManualUnitSelectionId={unitId}", dialogName, cueName, answerName, isExitAnswer, manualUnitSelectionId);
            if (_game.Dialog == null)
            {
                _logger.LogError("Current dialog is null");
                return false;
            }

            if (!string.Equals(_game.Dialog.Name, dialogName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(_game.Dialog.CurrentCueName, cueName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Dialog answer mismatch. ExpectedDialogName={expectedDialogName}, ExpectedCueName={expectedCueName}, ActualDialogName={actualDialogName}, ActualCueName={actualCueName}", _game.Dialog.Name, _game.Dialog.CurrentCueName, dialogName, cueName);
                return false;
            }

            // answer could be set from host notifications only
            // so it means we have a response from host and shouldn't skip default game logic
            if (_game.Dialog.Answer != null && string.Equals(answerName, _game.Dialog.Answer.AnswerName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Proceeding with dialog answer without extra steps. DialogName={dialogName}, CueName={cueName}, AnswerName={answerName}", dialogName, cueName, answerName);
                return true;
            }

            var message = new DialogCueAnswerSuggested { DialogName = dialogName, CueName = cueName, AnswerName = answerName };
            _logger.LogInformation("Sending dialog answer suggestion. DialogName={dialogName}, CueName={cueName}, AnswerName={answerName}", message.DialogName, message.CueName, message.AnswerName);
            _networkServerClient.SendAsync(message).Wait();

            return false;
        }

        public bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
        {
            if (string.Equals(_game.Dialog?.Name, dialogName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Dialog has been initiated, proceeding with default game logic.  DialogName={dialogName}", dialogName);
                return true;
            }

            _logger.LogInformation("Sending dialog request to host. DialogueName={dialogName}", dialogName);
            var message = new StartDialogRequested
            {
                DialogName = dialogName,
                TargetUnitId = targetUnitId,
                InitiatorUnitId = initiatorUnitId,
                MapObjectId = mapObjectId,
                SpeakerKey = speakerKey
            };
            _networkServerClient.SendAsync(message).Wait();
            return false;
        }

        private void SoftReset()
        {
            _game.Dialog = null;
            _game.Save = null;
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
                .Register<NotifyGamePauseChanged>(OnNotifyGamePauseChanged)
                .Register<NotifyPartyLeaveArea>(OnNotifyPartyLeaveArea)
                .Register<NotifyDialogCueAnswerSuggested>(OnNotifyDialogCueAnswerSuggested)
                .Register<NotifyDialogCueAnswerSelected>(OnNotifyDialogCueAnswerSelected)
                .Register<NotifyDialogStarted>(OnNotifyDialogStarted)
                ;

            _networkServerClient.OnError = OnNetworkClientError;
            _networkServerClient.OnConnected = OnNetworkClientConnected;
        }

        private async void OnNotifyDialogStarted(NotifyDialogStarted started)
        {
            _logger.LogInformation("Received NotifyDialogStarted.  DialogueName={dialogName},  TargetUnitId={targetId}, InitiatorUnitId={initiatorId}", started.DialogName, started.TargetUnitId, started.InitiatorUnitId);
            if (_game.Dialog == null || _game.Dialog.Name != started.DialogName)
            {
                _logger.LogInformation("New dialog has been initiated. PreviousDialog={previousDialogName}, CurrentDialogName={dialogName}", _game.Dialog?.Name, started.DialogName);
                _game.Dialog = new NetworkDialog(started.DialogName);
            }

            var hasStartedDialog = await _gameInteractionService.StartDialogAsync(started.DialogName, started.TargetUnitId, started.InitiatorUnitId, started.MapObjectId, started.SpeakerKey);
            if (!hasStartedDialog)
            {
                _logger.LogWarning("Client dialog is already started. DialogName={dialogName}", started.DialogName);
            }
        }

        private void OnNotifyDialogCueAnswerSelected(NotifyDialogCueAnswerSelected selected)
        {
            _logger.LogInformation("Received NotifyDialogCueAnswerSelected. DialogName={dialogName}, CueName={cueName}, AnswerName={answerName}", selected.DialogName, selected.CueName, selected.AnswerName);
            if (_game.Dialog == null)
            {
                _logger.LogError("Received dialog answer selection, but there is no active dialog right now. SuggestedDialogName={suggestedDialogName}, SuggestedCueName={suggestedCueName}, SuggestedAnswer={suggestedAnswerName}", selected.DialogName, selected.CueName, selected.AnswerName);
                return;
            }

            if (!string.Equals(_game.Dialog.Name, selected.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Dialog answer selection has mismatched dialog name. SuggestedDialogName={suggestedDialogName}, CurrentDialogName={currentCueName}", selected.DialogName, _game.Dialog.Name);
                return;
            }

            if (!string.Equals(_game.Dialog.CurrentCueName, selected.CueName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Dialog answer selection has mismatched cue. SuggestedCueName={suggestedCueName}, CurrentCueName={currentCueName}", selected.CueName, _game.Dialog.CurrentCueName);
                return;
            }

            _game.Dialog.Answer = new NetworkDialogAnswer
            {
                AnswerName = selected.AnswerName,
                CueName = selected.CueName,
                ManualUnitSelectionId = selected.ManualUnitSelectionId,
            };

            _gameInteractionService.SelectDialogAnswer(selected.DialogName, selected.CueName, selected.AnswerName, selected.ManualUnitSelectionId);
        }

        private void OnNotifyDialogCueAnswerSuggested(NotifyDialogCueAnswerSuggested suggested)
        {
            _logger.LogInformation("Received NotifyDialogCueAnswerSuggested. DialogName={dialogName}, CueName={cueName}, Suggestions={suggestionsCount}", suggested.DialogName, suggested.CueName, suggested.Suggestions.Count);

            if (_game.Dialog == null)
            {
                _logger.LogError("Received dialog answer suggestion, but there is no active dialog right now. SuggestedDialogName={suggestedDialogName}, SuggestedCueName={suggestedCueName}, SuggestedAnswer={suggestedAnswerName}", suggested.DialogName, suggested.CueName);
                return;
            }

            if (!string.Equals(_game.Dialog.Name, suggested.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Dialog suggestion has mismatched dialog name. SuggestedDialogName={suggestedDialogName}, CurrentDialogName={currentCueName}", suggested.DialogName, _game.Dialog.Name);
                return;
            }

            if (!string.Equals(_game.Dialog.CurrentCueName, suggested.CueName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Dialog suggestion has mismatched dialog. SuggestedCueName={suggestedCueName}, CurrentCueName={currentCueName}", suggested.CueName, _game.Dialog.CurrentCueName);
                return;
            }

            List<NetworkDialogAnswerSuggestion> suggestions = [.. suggested.Suggestions.Select(x => new NetworkDialogAnswerSuggestion { AnswerName = x.AnswerName, Players = [.. x.Players] })];
            _gameInteractionService.MarkSuggestedDialogAnswers(suggestions);
        }

        private void OnNotifyPartyLeaveArea(NotifyPartyLeaveArea area)
        {
            _logger.LogInformation("OnNotifyPartyLeaveArea. AreaExitId={areaExitId}", area.AreaExitId);
            _gameInteractionService.LeaveArea(area.AreaExitId);
        }

        private void OnNotifyGamePauseChanged(NotifyGamePauseChanged changed)
        {
            _logger.LogInformation("NotifyGamePauseChanged. Value={value}", changed.IsPaused);
            _gameInteractionService.Pause(changed.IsPaused);
        }

        private void OnNotifyCharacterMove(NotifyCharacterMove move)
        {
            _logger.LogInformation("Received NotifyCharacterMove. CharacterName={characterName}, DestinationX={x}, DestinationY={y}, DestinationZ={z}", move.CharacterName, move.DestinationX, move.DestinationY, move.DestinationZ);

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
            _logger.LogInformation("NotifyCharactersOwnerChanged. OwnersCount={ownersCount}", changed.Owners.Count);
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
    }
}
