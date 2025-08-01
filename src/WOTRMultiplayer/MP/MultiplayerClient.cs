using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerClient : MultiplayerActorBase, IMultiplayerClient
    {
        private readonly IIPEndPointParser _ipEndPointParser;
        private readonly INetworkServerClient _networkServerClient;

        public Action<string> OnNetworkError { get; set; }

        public Action<NetworkGameConnectivity> OnConnected { get; set; }

        public Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }
        public Action<List<NetworkCharacterOwnership>> OnGameCharactersChanged { get; set; }
        public Action<int, int> OnCharacterOwnerChanged { get; set; }
        public Action<string> OnStartGame { get; set; }

        public Action OnDisconnected { get; set; }

        public bool IsActive => _networkServerClient.IsActive;

        public bool IsConnecting => _networkServerClient.IsConnecting;

        private NetworkGameStage Status => Game?.Stage ?? NetworkGameStage.None;

        public bool IsInLobby => IsActive && Status == NetworkGameStage.Lobby;

        protected override bool IsHost => false;

        public MultiplayerClient(
            ILogger<MultiplayerClient> logger,
            IGameInteractionService gameInteractionService,
            IIPEndPointParser ipEndPointParser,
            IMultiplayerSettingsProvider multiplayerSettingsProvider,
            IFileSystemService fileSystemService,
            INetworkServerClient networkServerClient,
            IDiceRollStorage diceRollStorage,
            IMapper mapper) : base(logger, mapper, multiplayerSettingsProvider, gameInteractionService, diceRollStorage, fileSystemService)
        {
            _ipEndPointParser = ipEndPointParser;
            _networkServerClient = networkServerClient;
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
            Logger.LogInformation("Toggling ready status changed");
            var player = Game.Players.First(p => p.Id == Game.LocalPlayerId);
            player.IsReady = !player.IsReady;
            var readyChanged = new PlayerReadyStatusChanged { PlayerId = player.Id, IsReady = player.IsReady };
            _networkServerClient.Send(readyChanged);
            return readyChanged.IsReady;
        }

        public void Dispose()
        {
            Logger.LogInformation("Disposing");

            Game?.Reset();

            _networkServerClient?.Dispose();
        }

        public void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation)
        {
            if (Game.Combat != null)
            {
                return;
            }

            Logger.LogInformation("Sending CharacterMove. UnitId={unitId}, Destination={destination}, Delay={delay}, Orientation={orientation}", unitId, destination, delay, orientation);
            var message = new CharacterMove
            {
                UnitId = unitId,
                Destination = new Networking.Messages.NetworkVector3(destination.X, destination.Y, destination.Z),
                Delay = delay,
                Orientation = orientation
            };
            _networkServerClient.Send(message);
        }

        public void GameLoaded()
        {
            Logger.LogInformation("Game loaded");

            // assumption: should be done after each area load aswell
            SoftReset();

            GameInteraction.Pause(true);

            _networkServerClient.Send(new ClientGameLoaded());
        }

        /// <summary>
        /// Reloads current party characters and tries to merge ownership
        /// </summary>
        public void PartyChanged()
        {
            Logger.LogInformation("Updating current characters & merging ownership");

            // could be synced from host, but state is the same anyway
            var partyCharacters = GameInteraction.GetPartyPlayers();
            if (partyCharacters.Count == 0)
            {
                return;
            }

            var oldCharacters = Game.Characters.ToList();
            Game.Characters = [.. partyCharacters];
            var defaultOwner = GetPlayer(LocalHostPlayerId);
            foreach (var character in Game.Characters)
            {
                var existingOwnershipConfiguration = oldCharacters.FirstOrDefault(old =>
                    old.Name == character.Name || old.Name.Contains(character.Name));
                if (existingOwnershipConfiguration?.Owner != null)
                {
                    character.Owner = existingOwnershipConfiguration.Owner;
                    Logger.LogInformation("Character ownership has been preserved. UnitId={unitId}, CharacterName={characterName}, Owner={ownerId}", character.UnitId, character.Name, character.Owner.Id);
                    continue;
                }

                character.Owner = defaultOwner;
                Logger.LogInformation("Character ownership has been assigned to default player (host). UnitId={unitId}, CharacterName={characterName}, Owner={ownerId}", character.UnitId, character.Name, character.Owner.Id);
            }
        }

        public void Pause()
        {
            //Logger.LogInformation("Sending pausing notification");

            //var message = new GamePauseChanged { IsPaused = true };
            //_networkServerClient.SendAsync(message).Wait();
        }

        public void Unpause()
        {
            //Logger.LogInformation("Sending unpausing notification");
            //var message = new GamePauseChanged { IsPaused = false };
            //_networkServerClient.SendAsync(message).Wait();
        }

        public void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer)
        {
            Logger.LogInformation("Showing dialog Cue. DialogName={dialogName}, CueName={cueName}, HasSystemAnswer={hasSystemAnswer}", dialogName, cueName, hasSystemAnswer);
            if (hasSystemAnswer)
            {
                GameInteraction.SetDialogContinueButtonState(false);
            }

            if (Game.Dialog != null && Game.Dialog.Name != dialogName)
            {
                Logger.LogWarning("Previous dialog has not been disposed correctly. PreviousDialogName={previousDialogName}, CurrentDialogName={currentDialogName}", Game.Dialog.Name, dialogName);
                Game.Dialog = null;
            }

            Game.Dialog ??= new NetworkDialog(dialogName);
            Game.Dialog.CurrentCueName = cueName;
            Game.Dialog.Answer = null;

            GameInteraction.MarkSuggestedDialogAnswers([]);

            var message = new CueWitnessed { CueName = cueName, DialogName = dialogName };
            _networkServerClient.Send(message);
        }

        public bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId)
        {
            Logger.LogInformation("Select Dialog Answer. DialogName={dialogName}, CueName={cueName}, Answer={answer}, IsExitAnswer={isExitAnswer}, ManualUnitSelectionId={unitId}", dialogName, cueName, answerName, isExitAnswer, manualUnitSelectionId);
            if (Game.Dialog == null)
            {
                Logger.LogError("Current dialog is null");
                return false;
            }

            if (!string.Equals(Game.Dialog.Name, dialogName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Game.Dialog.CurrentCueName, cueName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog answer mismatch. ExpectedDialogName={expectedDialogName}, ExpectedCueName={expectedCueName}, ActualDialogName={actualDialogName}, ActualCueName={actualCueName}", Game.Dialog.Name, Game.Dialog.CurrentCueName, dialogName, cueName);
                return false;
            }

            // answer could be set from host notifications only
            // so it means we have a response from host and shouldn't skip default game logic
            if (Game.Dialog.Answer != null && string.Equals(answerName, Game.Dialog.Answer.AnswerName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Proceeding with dialog answer without extra steps. DialogName={dialogName}, CueName={cueName}, AnswerName={answerName}", dialogName, cueName, answerName);
                return true;
            }

            var message = new DialogCueAnswerSuggested { DialogName = dialogName, CueName = cueName, AnswerName = answerName };
            Logger.LogInformation("Sending dialog answer suggestion. DialogName={dialogName}, CueName={cueName}, AnswerName={answerName}", message.DialogName, message.CueName, message.AnswerName);
            _networkServerClient.Send(message);

            return false;
        }

        public bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
        {
            if (string.Equals(Game.Dialog?.Name, dialogName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Dialog has been initiated, proceeding with default game logic.  DialogName={dialogName}", dialogName);
                return true;
            }

            Logger.LogInformation("Sending dialog request to host. DialogueName={dialogName}", dialogName);
            var message = new StartDialogRequested
            {
                DialogName = dialogName,
                TargetUnitId = targetUnitId,
                InitiatorUnitId = initiatorUnitId,
                MapObjectId = mapObjectId,
                SpeakerKey = speakerKey
            };
            _networkServerClient.Send(message);
            return false;
        }

        public void CombatStarted()
        {
            Logger.LogInformation("Combat started");
            if (Game.Combat != null)
            {
                Logger.LogWarning("Previous combat has not been disposed correctly");
            }

            Game.Combat = new NetworkCombat();
        }

        public void CombatEnded()
        {
            Logger.LogInformation("Combat ended");
            if (Game.Combat == null)
            {
                Logger.LogWarning("Combat has not been started correctly");
            }

            Game.Combat = null;
        }

        public bool CanInitializeCombat()
        {
            // confirmation from host is required
            if (Game.Combat == null)
            {
                return true;
            }

            if (Game.Combat.IsInitialized)
            {
                Game.Combat.IsCombatPrepared = true;
            }

            return Game.Combat.IsInitialized;
        }

        public bool CanContinueCombat()
        {
            // must be run after Preparation phase
            return Game.Combat != null && Game.Combat.IsCombatPrepared;
        }

        public bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound)
        {
            try
            {
                return OnTurnStart(unitId, actingInSurpriseRound);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Unable to process {nameof(OnBeforeStartTurn)}. UnitId={{unitId}}, ActingInSurpriseRound={{actingInSurpriseRound}}", unitId, actingInSurpriseRound);
                throw;
            }
        }

        public bool OnBeforeEndTurn(string unitId)
        {
            try
            {
                return OnTurnEnd();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Unable to process {nameof(OnBeforeEndTurn)}. UnitId={{unitId}},", unitId);
                throw;
            }
        }

        public void ForceLoadGame(string savePath)
        {
            if (!string.IsNullOrEmpty(Game.SaveFilePath))
            {
                return;
            }

            Logger.LogInformation("Sending to host force load. SavePath={savePath}", savePath);
            Game.SaveFilePath = savePath;
            var message = new NotifySaveGameAssigned
            {
                Content = FileSystem.GetFile(savePath),
                IsForceLoad = true
            };

            _networkServerClient.Send(message);
        }

        public bool ShouldStoreRoll(bool silent)
        {
            return !IsRolledByHost(silent) && IsRolledByLocalPlayer(silent);
        }

        protected override Task<DiceRollValueResponse> RetrieveRoll(DiceRollValueRequest request, string unitId)
        {
            return _networkServerClient.SendAndWaitForAsync<DiceRollValueResponse>(request);
        }

        protected override void Send(object message)
        {
            _networkServerClient.Send(message);
        }

        protected override void OnLocalPlayerTurnEnded()
        {
            var message = new PlayerCombatTurnEnded { Round = Game.Combat.Round, UnitId = Game.Combat.Turn.UnitId };
            _networkServerClient.Send(message);
        }

        protected override void OnLocalPlayerTurnStart()
        {
            var message = new ClientCombatTurnStarted
            {
                UnitId = Game.Combat.Turn.UnitId,
                Round = Game.Combat.Round
            };

            _networkServerClient.Send(message);
        }

        private void SoftReset()
        {
            Game.Dialog = null;
            Game.SaveFilePath = null;
            Game.Combat = null;
        }

        private void RegisterHandlers()
        {
            _networkServerClient
                // this is kinda special as well as the host is blocking the game loop thread until `RollResponse` is received
                .Register<DiceRollValueRequest>(OnRollRequest)

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
                .Register<NotifyUnitClicked>(OnNotifyUnitClicked)
                .Register<NotifyGroundClicked>(OnNotifyGroundClicked)
                .Register<NotifyMapObjectClicked>(OnNotifyMapObjectClicked)
                .Register<NotifyAbilityUse>(OnNotifyAbilityUsed)
                .Register<NotifyToggleActivatableAbility>(OnNotifyToggleActivatableAbility)
                // combat
                .Register<PlayerCombatTurnEnded>(OnPlayerCombatTurnEnded)
                .Register<NotifyCombatStarted>(OnNotifyCombatStarted)
                .Register<NotifyCombatTurnStarted>(OnNotifyCombatTurnStarted)
                .Register<NotifyCombatTurnSynchronizationRequired>(OnNotifyCombatTurnSynchronizationRequired)
                ;

            _networkServerClient.OnError = OnNetworkClientError;
            _networkServerClient.OnConnected = OnNetworkClientConnected;
        }

        private void OnNotifyToggleActivatableAbility(NotifyToggleActivatableAbility toggle)
        {
            Logger.LogInformation($"Received {nameof(NotifyToggleActivatableAbility)}. AbilityId={{abilityId}}, IsActive={{isActive}}", toggle.Ability.Id, toggle.Ability.IsActive);
            var ability = Mapper.Map<NetworkActivatableAbility>(toggle.Ability);
            GameInteraction.ToggleActivatableAbility(ability);
        }

        private void OnNotifyAbilityUsed(NotifyAbilityUse used)
        {
            Logger.LogInformation($"Received {nameof(NotifyAbilityUse)}. AbilityId={{abilityId}}", used.Ability.Id);
            var ability = Mapper.Map<NetworkAbility>(used.Ability);
            GameInteraction.UseAbility(ability);
        }

        private async void OnNotifyCombatTurnSynchronizationRequired(NotifyCombatTurnSynchronizationRequired required)
        {
            try
            {
                Logger.LogInformation($"Received {nameof(NotifyCombatTurnSynchronizationRequired)}. Units={{unitsCount}}", required.Units.Count);

                await SynchronizeUnitsAsync(required.Units);

                Logger.LogInformation($"Units have been synchronized. Sending {nameof(ClientCombatTurnSynchronized)} confirmation");
                var message = new ClientCombatTurnSynchronized { Round = Game.Combat.Round, UnitId = Game.Combat.Turn.UnitId };
                _networkServerClient.Send(message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to sync units");
                throw;
            }
        }

        private void OnPlayerCombatTurnEnded(PlayerCombatTurnEnded ended)
        {
            Logger.LogInformation($"Received {nameof(PlayerCombatTurnEnded)}. Round={{round}}, UnitId={{unitId}}", ended.Round, ended.UnitId);
            if (Game.Combat.Round == ended.Round && string.Equals(Game.Combat.Turn?.UnitId, ended.UnitId, StringComparison.OrdinalIgnoreCase))
            {
                EndLocalTurn();
            }
        }

        private void OnNotifyGroundClicked(NotifyGroundClicked clicked)
        {
            Logger.LogInformation($"Received {nameof(NotifyGroundClicked)}. SelectedUnitId={{selectedUnits}}, WorldPosition={{worldPosition}}", clicked.Click.SelectedUnits.Count, clicked.Click.WorldPosition);
            if (Game.Combat == null)
            {
                Logger.LogWarning($"{nameof(NotifyGroundClicked)} is ignored out of combat");
                return;
            }

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickGroundInCombat(click);
        }

        private void OnNotifyUnitClicked(NotifyUnitClicked clicked)
        {
            Logger.LogInformation($"Received {nameof(NotifyUnitClicked)}. TargetUnitId={{targetUnitId}}, SelectedUnits={{selectedUnits}}", clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);

            if (Game.Combat == null)
            {
                Logger.LogInformation($"{nameof(NotifyUnitClicked)} is ignored out of combat");
                return;
            }

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickUnitInCombat(click);
        }

        private void OnNotifyMapObjectClicked(NotifyMapObjectClicked clicked)
        {
            Logger.LogInformation($"Received {nameof(NotifyMapObjectClicked)}.TargetUnitId={{targetUnitId}}, SelectedUnits={{selectedUnits}}", clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickMapObject(click);
        }

        private async void OnRollRequest(DiceRollValueRequest request)
        {
            Logger.LogInformation($"Received {nameof(DiceRollValueRequest)}. RollId={{rollId}}", request.RollId);
            // only host could ask for a roll since there is no direct connection between clients
            var response = await GetLocalRollAsync(LocalHostPlayerId, request);
            _networkServerClient.Send(response);
        }

        private void OnNotifyCombatTurnStarted(NotifyCombatTurnStarted started)
        {
            Logger.LogInformation($"Received {nameof(NotifyCombatTurnStarted)}. Round={{round}}, UnitId={{unitId}}", started.Round, started.UnitId);
            if (Game.Combat?.Turn == null)
            {
                Logger.LogError("Trying to start not initialized turn. Round={round}, UnitId={unitId}", started.Round, started.UnitId);
                return;
            }

            if (!string.Equals(started.UnitId, Game.Combat.Turn.UnitId))
            {
                Logger.LogWarning("Starting turn with different UnitId. LocalUnitId={localUnitId}, HostUnitId={hostUnitId}", Game.Combat.Turn.UnitId, started.UnitId);
            }

            if (Game.Combat.Round != started.Round)
            {
                Logger.LogWarning("Starting turn with different Round number. LocalRound={localRound}, HostRound={hostRound}", Game.Combat.Round, started.Round);
            }

            Game.Combat.Turn.IsInProgress = true;
            GameInteraction.StartTurnBasedCombatTurn(Game.Combat.Turn.IsActingInSurpriseRound);
        }

        private async void OnNotifyCombatStarted(NotifyCombatStarted started)
        {
            Logger.LogInformation($"Received {nameof(NotifyCombatStarted)}. Units={{unitsCount}}", started.Units.Count);

            if (Game.Combat == null)
            {
                Logger.LogWarning("Combat has not been started on client yet. Waiting until start");
                while (Game.Combat == null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }

            await SynchronizeUnitsAsync(started.Units);

            Game.Combat.IsInitialized = true;

            Logger.LogInformation($"Sending {nameof(ClientCombatInitialized)}");
            var message = new ClientCombatInitialized();
            _networkServerClient.Send(message);
        }

        private async Task SynchronizeUnitsAsync(List<Networking.Messages.NetworkUnit> units)
        {
            var unitsToSync = Mapper.Map<List<NetworkUnit>>(units);

            await GameInteraction.UpdateUnitsAsync(unitsToSync);
        }

        private async void OnNotifyDialogStarted(NotifyDialogStarted started)
        {
            Logger.LogInformation($"Received {nameof(NotifyDialogStarted)}.  DialogueName={{dialogName}},  TargetUnitId={{targetId}}, InitiatorUnitId={{initiatorId}}", started.DialogName, started.TargetUnitId, started.InitiatorUnitId);
            if (Game.Dialog == null || Game.Dialog.Name != started.DialogName)
            {
                Logger.LogInformation("New dialog has been initiated. PreviousDialog={previousDialogName}, CurrentDialogName={dialogName}", Game.Dialog?.Name, started.DialogName);
                Game.Dialog = new NetworkDialog(started.DialogName);
            }

            var hasStartedDialog = await GameInteraction.StartDialogAsync(started.DialogName, started.TargetUnitId, started.InitiatorUnitId, started.MapObjectId, started.SpeakerKey);
            if (!hasStartedDialog)
            {
                Logger.LogWarning("Client dialog is already started. DialogName={dialogName}", started.DialogName);
            }
        }

        private void OnNotifyDialogCueAnswerSelected(NotifyDialogCueAnswerSelected selected)
        {
            Logger.LogInformation($"Received {nameof(NotifyDialogCueAnswerSelected)}. DialogName={{dialogName}}, CueName={{cueName}}, AnswerName={{answerName}}", selected.DialogName, selected.CueName, selected.AnswerName);
            if (Game.Dialog == null)
            {
                Logger.LogError("Received dialog answer selection, but there is no active dialog right now. SuggestedDialogName={suggestedDialogName}, SuggestedCueName={suggestedCueName}, SuggestedAnswer={suggestedAnswerName}", selected.DialogName, selected.CueName, selected.AnswerName);
                return;
            }

            if (!string.Equals(Game.Dialog.Name, selected.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog answer selection has mismatched dialog name. SuggestedDialogName={suggestedDialogName}, CurrentDialogName={currentCueName}", selected.DialogName, Game.Dialog.Name);
                return;
            }

            if (!string.Equals(Game.Dialog.CurrentCueName, selected.CueName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog answer selection has mismatched cue. SuggestedCueName={suggestedCueName}, CurrentCueName={currentCueName}", selected.CueName, Game.Dialog.CurrentCueName);
                return;
            }

            Game.Dialog.Answer = new NetworkDialogAnswer
            {
                AnswerName = selected.AnswerName,
                CueName = selected.CueName,
                ManualUnitSelectionId = selected.ManualUnitSelectionId,
            };

            GameInteraction.SelectDialogAnswer(selected.DialogName, selected.CueName, selected.AnswerName, selected.ManualUnitSelectionId);
        }

        private void OnNotifyDialogCueAnswerSuggested(NotifyDialogCueAnswerSuggested suggested)
        {
            Logger.LogInformation($"Received {nameof(NotifyDialogCueAnswerSuggested)}. DialogName={{dialogName}}, CueName={{cueName}}, Suggestions={{suggestionsCount}}", suggested.DialogName, suggested.CueName, suggested.Suggestions.Count);

            if (Game.Dialog == null)
            {
                Logger.LogError("Received dialog answer suggestion, but there is no active dialog right now. SuggestedDialogName={suggestedDialogName}, SuggestedCueName={suggestedCueName}, SuggestedAnswer={suggestedAnswerName}", suggested.DialogName, suggested.CueName);
                return;
            }

            if (!string.Equals(Game.Dialog.Name, suggested.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog suggestion has mismatched dialog name. SuggestedDialogName={suggestedDialogName}, CurrentDialogName={currentCueName}", suggested.DialogName, Game.Dialog.Name);
                return;
            }

            if (!string.Equals(Game.Dialog.CurrentCueName, suggested.CueName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog suggestion has mismatched dialog. SuggestedCueName={suggestedCueName}, CurrentCueName={currentCueName}", suggested.CueName, Game.Dialog.CurrentCueName);
                return;
            }

            List<NetworkDialogAnswerSuggestion> suggestions = [.. suggested.Suggestions.Select(x => new NetworkDialogAnswerSuggestion { AnswerName = x.AnswerName, Players = [.. x.Players] })];
            GameInteraction.MarkSuggestedDialogAnswers(suggestions);
        }

        private void OnNotifyPartyLeaveArea(NotifyPartyLeaveArea area)
        {
            Logger.LogInformation($"Received {nameof(OnNotifyPartyLeaveArea)}. AreaExitId={{areaExitId}}", area.AreaExitId);
            GameInteraction.LeaveArea(area.AreaExitId);
        }

        private void OnNotifyGamePauseChanged(NotifyGamePauseChanged changed)
        {
            Logger.LogInformation($"Received {nameof(NotifyGamePauseChanged)}. Value={{value}}", changed.IsPaused);
            GameInteraction.Pause(changed.IsPaused);
        }

        private void OnNotifyCharacterMove(NotifyCharacterMove move)
        {
            Logger.LogInformation($"Received {nameof(NotifyCharacterMove)}. UnitId={{UnitId}}, Destination={{destination}}", move.UnitId, move.Destination);

            var destination = new NetworkVector3(move.Destination.X, move.Destination.Y, move.Destination.Z);
            GameInteraction.MoveNonCombatCharacter(move.UnitId, destination, move.Delay, move.Orientation);
        }

        private void OnNotifyGameStarted(NotifyGameStarted started)
        {
            Logger.LogInformation($"Received {nameof(NotifyGameStarted)}");
            if (string.IsNullOrEmpty(Game.SaveFilePath))
            {
                Logger.LogCritical("Trying to start a game with missing save file path");
                return;
            }

            OnStartGame?.Invoke(Game.SaveFilePath);
        }

        private void OnNotifyCharactersOwnerChanged(NotifyCharactersOwnerChanged changed)
        {
            Logger.LogInformation($"Received {nameof(NotifyCharactersOwnerChanged)}. OwnersCount={{ownersCount}}", changed.Owners.Count);
            try
            {
                for (int i = 0; i < changed.Owners.Count; i++)
                {
                    var owner = changed.Owners[i];
                    var player = Game.Players.FirstOrDefault(p => p.Id == owner.PlayerId);
                    if (player == null)
                    {
                        Logger.LogWarning("Unable to assign character ownership for missing player. PlayerId={playerId}", owner.PlayerId);
                        player = Game.Players.First();
                    }

                    Game.Characters[owner.CharacterIndex].Owner = player;
                    OnCharacterOwnerChanged?.Invoke(owner.CharacterIndex, Game.Players.IndexOf(player));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to handle changed character ownership");
                throw;
            }
        }

        private void OnNotifyGameStageChanged(NotifyGameStageChanged changed)
        {
            Logger.LogInformation($"Received {nameof(NotifyGameStageChanged)}. Status={{newGameStatus}}", changed.Stage);
            Game.Stage = (NetworkGameStage)Enum.Parse(typeof(NetworkGameStage), changed.Stage, true);
        }

        private void OnNotifySaveGameAssigned(NotifySaveGameAssigned assigned)
        {
            Logger.LogInformation($"Received {nameof(NotifySaveGameAssigned)}. GameStatus={{status}}, Size={{contentSize}}, IsForceLoad={{isForceLoad}}", Game.Stage, assigned.Content.Length, assigned.IsForceLoad);

            var baseUnityPath = GameInteraction.GetSaveGamePath();
            var multiplayerPath = Regex.Replace(baseUnityPath, "(((\\\\|\\/)+)(Saved Games)((\\\\|\\/)+))$", "/Saved Multiplayer Games/");
            var savePath = Path.Combine(multiplayerPath, "latest save.zks");
            Logger.LogInformation("Save game path changed. Path={path}", savePath);
            if (!FileSystem.WriteFile(savePath, assigned.Content))
            {
                Logger.LogError("Unable to store save game");
                // on error?
                return;
            }

            Game.SaveFilePath = savePath;

            if (assigned.IsForceLoad)
            {
                Logger.LogInformation("Force loading save game. SavePath={savePath}", savePath);
                GameInteraction.QuickLoadGame(savePath);
                return;
            }

            Logger.LogInformation("Game is ready to be started. SavePath={savePath}", savePath);
            _networkServerClient.Send(new PlayerSaveGameSyncChanged { IsSynced = true });

        }

        private void OnPlayerReadyStatusChanged(PlayerReadyStatusChanged readyStatusChanged)
        {
            Logger.LogInformation($"Received {nameof(PlayerReadyStatusChanged)}. PlayerId={{playerId}}, IsReady={{isReady}}", readyStatusChanged.PlayerId, readyStatusChanged.IsReady);

            lock (ActionLock)
            {
                var existingPlayer = GetPlayer(readyStatusChanged.PlayerId);
                if (existingPlayer == null)
                {
                    Logger.LogWarning("Can't find existing player. PlayerId={playerId}", readyStatusChanged.PlayerId);
                    return;
                }

                existingPlayer.IsReady = readyStatusChanged.IsReady;
                OnPlayersChanged?.Invoke(Game.Players);
            }
        }

        private void OnNotifyGameCharactersChanged(NotifyGameCharactersChanged changed)
        {
            Logger.LogInformation($"Received {nameof(NotifyGameCharactersChanged)}. Portraits={{portraits}}", string.Join(";", changed.Characters.Select(c => c.Portrait)));
            Game.Characters.Clear();
            Game.Characters.AddRange(changed.Characters.Select(c => new NetworkCharacterOwnership { Name = c.Name, Portrait = c.Portrait }));
            OnGameCharactersChanged?.Invoke(Game.Characters);
        }

        private void OnNotifyPlayersChanged(NotifyPlayersChanged changed)
        {
            Logger.LogInformation($"Received {nameof(NotifyPlayersChanged)}. PlayersCount={{playersCount}}", nameof(NotifyPlayersChanged), changed.Players.Count);
            Game.Players.Clear();
            var players = changed.Players.Select(p => new NetworkPlayer(p.Id) { IsReady = p.IsReady, Name = p.Name }).ToList();
            Game.Players.AddRange(players);

            // add or remove players should cause owner reset
            foreach (var character in Game.Characters)
            {
                character.Owner = Game.Players.First();
            }

            OnPlayersChanged?.Invoke(Game.Players);
        }

        private void OnNetworkClientConnected(EndPoint endpoint)
        {
            Game = new NetworkGame(null)
            {
                Connectivity = new NetworkGameConnectivity
                {
                    Endpoint = endpoint
                }
            };
            OnConnected?.Invoke(Game.Connectivity);
        }

        private void OnNetworkClientError(Exception exception)
        {
            if (exception is SocketException socketException)
            {
                string error = string.Empty;
                switch (socketException.SocketErrorCode)
                {
                    case SocketError.OperationAborted: // client disconnected by a user
                        Logger.LogWarning("Skipping notification. SocketCode={socketCode}", socketException.SocketErrorCode);
                        break;
                    case SocketError.ConnectionReset:
                    case SocketError.Success:
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
            Logger.LogError(exception, "Generic network error occurred");
            InvokeOnNetworkError("Generic network error occurred.");
        }

        private void InvokeOnNetworkError(string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                return;
            }

            OnNetworkError?.Invoke(error);
            GameInteraction.ShowModalMessage(error);
        }

        private void OnPlayerNameRequest(PlayerNameRequest request)
        {
            Logger.LogInformation($"Received {nameof(PlayerNameRequest)}. ClientPlayerId={{clientPlayerId}}", request.ClientPlayerId);
            if (Game == null)
            {
                Logger.LogError("Game has not been initialized yet");
                return;
            }

            Game.LocalPlayerId = request.ClientPlayerId;

            var nameResponse = new PlayerNameResponse() { Name = SettingsProvider.Settings.PlayerName };
            _networkServerClient.Send(nameResponse);
            Logger.LogInformation("Player name has been sent. Name={name}", nameResponse.Name);
        }
    }
}
