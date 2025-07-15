using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
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
        private readonly INetworkServerClient _networkServerClient;
        private readonly IMultiplayerSettingsProvider _multiplayerSettingsProvider;
        private readonly IGameInteractionService _gameInteractionService;
        public const int LocalHostPlayerId = -1;

        private NetworkGame _game;
        private readonly object _actionlock = new();

        public Action<string> OnNetworkError { get; set; }

        public Action<EndPoint> OnConnected { get; set; }

        public Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }
        public Action<List<NetworkCharacterOwnership>> OnGameCharactersChanged { get; set; }
        public Action<int, int> OnCharacterOwnerChanged { get; set; }
        public Action<string> OnStartGame { get; set; }

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
            IFileSystemService fileSystemService,
            INetworkServerClient networkServerClient)
        {
            _logger = logger;
            _ipEndPointParser = ipEndPointParser;
            _fileSystemService = fileSystemService;
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
            var player = _game.Players.First(p => p.Id == _game.LocalPlayerId);
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

        public bool CanControlCharacter(string unitId)
        {
            if (_game == null)
            {
                return false;
            }

            var realCharacterId = _gameInteractionService.GetPetOwnerId(unitId) ?? unitId;

            var character = _game.Characters.FirstOrDefault(c => string.Equals(c.UnitId, realCharacterId, StringComparison.OrdinalIgnoreCase));
            if (character == null)
            {
                _logger.LogWarning("Unable to find character in the list. UnitId={unitId}", realCharacterId);
                return false;
            }

            return character.Owner != null && character.Owner.Id == _game.LocalPlayerId;
        }

        public void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation)
        {
            // TODO: current trigger couldn't be used in combat
            if (_game.Combat != null)
            {
                return;
            }

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
                    _logger.LogInformation("Character ownership has been preserved. UnitId={unitId}, CharacterName={characterName}, Owner={ownerId}", character.UnitId, character.Name, character.Owner.Id);
                    continue;
                }

                character.Owner = defaultOwner;
                _logger.LogInformation("Character ownership has been assigned to default player (host). UnitId={unitId}, CharacterName={characterName}, Owner={ownerId}", character.UnitId, character.Name, character.Owner.Id);
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

        /// <summary>
        ///
        /// </summary>
        /// <param name="networkDiceRollId"></param>
        /// <param name="unitId">client doesn't really care about unitId since it has connection to host only</param>
        /// <returns></returns>
        public NetworkDiceRoll RetrieveRoll(int networkDiceRollId, string unitId)
        {
            _logger.LogInformation("Retrieving roll from the host. RollId={rollId}, UnitId={unitId}", networkDiceRollId, unitId);

            var request = new RollRequest { RollId = networkDiceRollId };
            // it's important to block current thread since we cannot proceed without response
            // yeah most likely it will cause the game to freeze in case of bad network
            var response = _networkServerClient.SendAndWaitForAsync<RollResponse>(request).Result;

            if (response == null)
            {
                _logger.LogError("Unable to retrieve roll from host. RollId={rollId}", networkDiceRollId);
                return null;
            }

            if (response.Roll == null)
            {
                _logger.LogError("Host returned null roll. RollId={rollId}", networkDiceRollId);
                return null;
            }

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

        public void CombatStarted()
        {
            _logger.LogInformation("Combat started");
            if (_game.Combat != null)
            {
                _logger.LogWarning("Previous combat has not been disposed correctly");
            }

            _game.Combat = new NetworkCombat();
        }

        public void CombatEnded()
        {
            _logger.LogInformation("Combat ended");
            if (_game.Combat == null)
            {
                _logger.LogWarning("Combat has not been started correctly");
            }

            _game.Combat = null;
        }

        public bool CanInitializeCombat()
        {
            // confirmation from host is required
            return _game.Combat.IsInitialized;
        }

        public bool CanContinueCombat()
        {
            // confirmation from host is required
            return _game.Combat.IsInitialized;
        }

        public bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound)
        {
            _game.Combat.TurnOwner = unitId;
            _game.Combat.IsMyTurn = CanControlCharacter(unitId);
            _game.Combat.IsAITurn = _gameInteractionService.IsUnitAI(unitId);
            _game.Combat.IsActingInSurpriseRound = actingInSurpriseRound;
            _logger.LogInformation("OnBeforeStartTurn. UnitId={unitId}, IsMyTurn={isMyTurn}, IsAITurn={isAITurn}, IsActingInSurpriseRound={isActingInSurpriseRound}", unitId, _game.Combat.IsMyTurn, _game.Combat.IsAITurn, _game.Combat.IsActingInSurpriseRound);

            return true;
        }

        public bool OnBeforeEndTurn(string unitId)
        {
            _logger.LogInformation("OnBeforeEndTurn. UnitId={unitId}, IsMyTurn={isMyTurn}, IsAITurn={isAITurn}", unitId, _game.Combat.IsMyTurn, _game.Combat.IsAITurn);
            _game.Combat.TurnOwner = null;
            _game.Combat.IsMyTurn = false;
            _game.Combat.IsAITurn = false;
            return true;
        }

        public void CombatRoundStarted(int round)
        {
            _logger.LogInformation("Combat round started. Round={round}", round);
            if (_game.Combat == null)
            {
                _logger.LogWarning("Combat has not started yet");
                return;
            }

            _game.Combat.Round = round;
        }

        public int GetCombatRound()
        {
            return _game.Combat?.Round ?? 0;
        }

        public void ForceLoadGame(string savePath)
        {
            _logger.LogInformation("Sending to host force load. SavePath={savePath}", savePath);
            var message = new NotifySaveGameAssigned
            {
                Content = _fileSystemService.GetFile(savePath),
                IsForceLoad = true
            };
            _networkServerClient.SendAsync(message).Wait();
        }

        public bool ShouldStoreRoll()
        {
            // client should store roll only in combat + on its turn
            return _game.Combat != null
                && _game.Combat.IsInitialized
                && !_game.Combat.IsAITurn
                && _game.Combat.IsMyTurn;
        }

        private void SoftReset()
        {
            _game.Dialog = null;
            _game.SaveFilePath = null;
            _game.Combat = null;
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
                .Register<NotifyCombatStarted>(OnNotifyCombatStarted)
                ;

            _networkServerClient.OnError = OnNetworkClientError;
            _networkServerClient.OnConnected = OnNetworkClientConnected;
        }

        private async void OnNotifyCombatStarted(NotifyCombatStarted started)
        {
            _logger.LogInformation($"Received {nameof(NotifyCombatStarted)}.  Units={{unitsCount}}", started.Units.Count);

            if (_game.Combat == null)
            {
                _logger.LogWarning("Combat has not been started on client yet. Waiting until start");
                while (_game.Combat == null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }

            // TODO: sync units
            _logger.LogError("Units are not synced");

            _game.Combat.IsInitialized = true;

            _logger.LogInformation($"Sending {nameof(ClientCombatInitialized)}");
            var message = new ClientCombatInitialized();
            _networkServerClient.SendAsync(message).Wait();
        }

        private async void OnNotifyDialogStarted(NotifyDialogStarted started)
        {
            _logger.LogInformation($"Received {nameof(NotifyDialogStarted)}.  DialogueName={{dialogName}},  TargetUnitId={{targetId}}, InitiatorUnitId={{initiatorId}}", started.DialogName, started.TargetUnitId, started.InitiatorUnitId);
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
            _logger.LogInformation($"Received {nameof(NotifyDialogCueAnswerSelected)}. DialogName={{dialogName}}, CueName={{cueName}}, AnswerName={{answerName}}", selected.DialogName, selected.CueName, selected.AnswerName);
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
            _logger.LogInformation($"Received {nameof(NotifyDialogCueAnswerSuggested)}. DialogName={{dialogName}}, CueName={{cueName}}, Suggestions={{suggestionsCount}}", suggested.DialogName, suggested.CueName, suggested.Suggestions.Count);

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
            _logger.LogInformation($"Received {nameof(OnNotifyPartyLeaveArea)}. AreaExitId={{areaExitId}}", area.AreaExitId);
            _gameInteractionService.LeaveArea(area.AreaExitId);
        }

        private void OnNotifyGamePauseChanged(NotifyGamePauseChanged changed)
        {
            _logger.LogInformation($"Received {nameof(NotifyGamePauseChanged)}. Value={{value}}", changed.IsPaused);
            _gameInteractionService.Pause(changed.IsPaused);
        }

        private void OnNotifyCharacterMove(NotifyCharacterMove move)
        {
            _logger.LogInformation($"Received {nameof(NotifyCharacterMove)}. CharacterName={{characterName}}, DestinationX={{x}}, DestinationY={{y}}, DestinationZ={{z}}", move.CharacterName, move.DestinationX, move.DestinationY, move.DestinationZ);

            var destination = new Vector3(move.DestinationX, move.DestinationY, move.DestinationZ);
            _gameInteractionService.MoveCharacter(move.CharacterName, destination, move.Delay, move.Orientation);
        }

        private void OnNotifyGameStarted(NotifyGameStarted started)
        {
            _logger.LogInformation($"Received {nameof(NotifyGameStarted)}");
            if (string.IsNullOrEmpty(_game.SaveFilePath))
            {
                _logger.LogCritical("Trying to start a game with missing save file path");
                return;
            }

            OnStartGame?.Invoke(_game.SaveFilePath);
        }

        private void OnNotifyCharactersOwnerChanged(NotifyCharactersOwnerChanged changed)
        {
            _logger.LogInformation($"Received {nameof(NotifyCharactersOwnerChanged)}. OwnersCount={{ownersCount}}", changed.Owners.Count);
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
                _logger.LogError(ex, "Unable to handle changed character ownership");
                throw;
            }
        }

        private void OnNotifyGameStageChanged(NotifyGameStageChanged changed)
        {
            _logger.LogInformation($"Received {nameof(NotifyGameStageChanged)}. Status={{newGameStatus}}", changed.Stage);
            _game.Stage = (NetworkGameStage)Enum.Parse(typeof(NetworkGameStage), changed.Stage, true);
        }

        private void OnNotifySaveGameAssigned(NotifySaveGameAssigned assigned)
        {
            _logger.LogInformation($"Received {nameof(NotifySaveGameAssigned)}. GameStatus={{status}}, Size={{contentSize}}, IsForceLoad={{isForceLoad}}", _game.Stage, assigned.Content.Length, assigned.IsForceLoad);

            var baseUnityPath = _gameInteractionService.GetSaveGamePath();
            var multiplayerPath = Regex.Replace(baseUnityPath, "(((\\\\|\\/)+)(Saved Games)((\\\\|\\/)+))$", "/Saved Multiplayer Games/");
            var savePath = Path.Combine(multiplayerPath, "latest save.zks");
            _logger.LogInformation("Save game path changed. Path={path}", savePath);
            if (!_fileSystemService.WriteFile(savePath, assigned.Content))
            {
                _logger.LogError("Unable to store save game");
                // on error?
                return;
            }

            _game.SaveFilePath = savePath;

            _logger.LogInformation("Game is ready to be started. SavePath={savePath}", savePath);
            _networkServerClient.SendAsync(new PlayerSaveGameSyncChanged { IsSynced = true }).Wait();

            if (assigned.IsForceLoad)
            {
                _logger.LogInformation("Force loading save game. SavePath={savePath}", savePath);
                _gameInteractionService.QuickLoadGame(savePath);
            }
        }

        private void OnPlayerReadyStatusChanged(PlayerReadyStatusChanged readyStatusChanged)
        {
            _logger.LogInformation($"Received {nameof(PlayerReadyStatusChanged)}. PlayerId={{playerId}}, IsReady={{isReady}}", readyStatusChanged.PlayerId, readyStatusChanged.IsReady);

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
            _logger.LogInformation($"Received {nameof(NotifyGameCharactersChanged)}. Portraits={{portraits}}", string.Join(";", changed.Characters.Select(c => c.Portrait)));
            _game.Characters.Clear();
            _game.Characters.AddRange(changed.Characters.Select(c => new NetworkCharacterOwnership { Name = c.Name, Portrait = c.Portrait }));
            OnGameCharactersChanged?.Invoke(_game.Characters);
        }

        private void OnNotifyPlayersChanged(NotifyPlayersChanged changed)
        {
            _logger.LogInformation($"Received {nameof(NotifyPlayersChanged)}. PlayersCount={{playersCount}}", nameof(NotifyPlayersChanged), changed.Players.Count);
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

                InvokeOnNetworkError(error);
                return;
            }

            // should never happen?
            _logger.LogError(exception, "Generic network error occurred");
            InvokeOnNetworkError("Generic network error occurred.");
        }

        private void InvokeOnNetworkError(string error)
        {
            OnNetworkError?.Invoke(error);
            _gameInteractionService.ShowModalMessage(error);
        }

        private void OnPlayerNameRequest(PlayerNameRequest request)
        {
            _logger.LogInformation($"Received {nameof(PlayerNameRequest)}. ClientPlayerId={{clientPlayerId}}", request.ClientPlayerId);
            if (_game == null)
            {
                _logger.LogError("Game has not been initialized yet");
                return;
            }

            _game.LocalPlayerId = request.ClientPlayerId;

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
