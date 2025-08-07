using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.MP.Actors;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer.MP.Actors
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
            IUniqueIdGenerator uniqueIdGenerator,
            IMapper mapper)
            : base(logger,
                  mapper,
                  multiplayerSettingsProvider,
                  gameInteractionService,
                  diceRollStorage,
                  fileSystemService,
                  uniqueIdGenerator)
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

            GameInteraction.Pause(true);

            _networkServerClient.Send(new ClientGameLoaded());
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

            Game.Dialog.CurrentCueName = cueName;
            Game.Dialog.Answer = null;

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

        public bool CanInitializeCombat()
        {
            // confirmation from host is required
            if (Game.Combat == null || !Game.Combat.IsInitialized)
            {
                return false;
            }

            PrepareCombat();

            return true;
        }

        public bool CanContinueCombat()
        {
            if (Game.Combat == null)
            {
                return false;
            }

            if (Game.Combat.IsCombatPrepared && Game.Combat.InitialCombatOrder.Count > 0)
            {
                GameInteraction.UpdateCombatOrder(Game.Combat.InitialCombatOrder);
                Game.Combat.InitialCombatOrder.Clear();
            }

            return Game.Combat.IsCombatPrepared;
        }

        public bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound)
        {
            try
            {
                return OnTurnStart(unitId, actingInSurpriseRound);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to process {nameof(OnBeforeStartTurn)}. UnitId={unitId}, ActingInSurpriseRound={actingInSurpriseRound}", unitId, actingInSurpriseRound);
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
                Logger.LogError(ex, "Unable to process {nameof(OnBeforeEndTurn)}. UnitId={unitId},", unitId);
                throw;
            }
        }

        public bool IsDiceRollOwner(bool silent)
        {
            return !IsRolledByHost(silent) && IsRolledByLocalPlayer(silent);
        }

        protected override Task<DiceRollValueResponse> RetrieveRollAsync(DiceRollValueRequest request, string unitId)
        {
            return _networkServerClient.SendAndWaitForAsync<DiceRollValueResponse>(request);
        }

        protected override void Send(object message)
        {
            _networkServerClient.Send(message);
        }

        protected override void Send(long playerId, object message)
        {
            Send(message);
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
                .Register<NotifyCombatInitialized>(OnNotifyCombatInitialized)
                .Register<NotifyCombatTurnStarted>(OnNotifyCombatTurnStarted)
                .Register<NotifyCombatTurnSynchronizationRequired>(OnNotifyCombatTurnSynchronizationRequired)

                .Register<NotifyContainerLooted>(OnNotifyContainerLooted)
                .Register<NotifyDropItem>(OnNotifyDropItem)
                .Register<NotifyEquipmentSlotChanged>(OnNotifyEquipmentSlotChanged)
                .Register<NotifyActiveHandEquipmentSetChanged>(OnNotifyActiveHandEquipmentSetChanged)

                .Register<NotifyOvertipInteracted>(OnNotifyOvertipInteracted)
                .Register<NotifyUnitJoinedMidCombat>(OnNotifyUnitJoinedMidCombat)
                .Register<NotifyPerceptionCheckRolled>(OnNotifyPerceptionCheckRolled)
                ;

            _networkServerClient.OnError = OnNetworkClientError;
            _networkServerClient.OnConnected = OnNetworkClientConnected;
        }

        private void OnNotifyPerceptionCheckRolled(NotifyPerceptionCheckRolled rolled)
        {
            Logger.LogInformation("Received {messageType}. UnitId={unitID}, MapObjectId={round}", nameof(NotifyPerceptionCheckRolled), rolled.Check.UnitId, rolled.Check.MapObject.Id);

            var check = Mapper.Map<NetworkPerceptionCheck>(rolled.Check);
            GameInteraction.ApplyPerceptionCheck(check);
        }

        private void OnNotifyUnitJoinedMidCombat(NotifyUnitJoinedMidCombat combat)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, UnitId={unitId}", nameof(NotifyUnitJoinedMidCombat), combat.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitJoinedMidCombat, combat.PlayerId, combat.UnitId);
        }

        private void OnNotifyOvertipInteracted(NotifyOvertipInteracted interacted)
        {
            Logger.LogInformation("Received {messageType}. MapObjectId={mapObjectId}, UnitsCount={unitsCount}", nameof(NotifyOvertipInteracted), interacted.Overtip.MapObject.Id, interacted.Overtip.Units);
            var overtip = Mapper.Map<NetworkOvertip>(interacted.Overtip);
            GameInteraction.InteractWithOvertip(overtip);
        }

        private void OnNotifyActiveHandEquipmentSetChanged(NotifyActiveHandEquipmentSetChanged changed)
        {
            Logger.LogInformation("Received {messageType}. UnitId={unitId}, SetIndex={setIndex}", nameof(NotifyEquipmentSlotChanged), changed.Set.UnitId, changed.Set.Index);
            var set = Mapper.Map<NetworkActiveHandEquipmentSet>(changed.Set);
            GameInteraction.SetActiveHandEquipmentSet(set);
        }

        private void OnNotifyEquipmentSlotChanged(NotifyEquipmentSlotChanged slotChanged)
        {
            Logger.LogInformation("Received {messageType}. SlotType={slotType}, SlotIndex={slotIndex}, ItemId={itemId}, OwnerId={ownerId}", nameof(NotifyEquipmentSlotChanged), slotChanged.Slot.Position.Type, slotChanged.Slot.Position.Index, slotChanged.Slot.ItemId, slotChanged.Slot.OwnerId);
            var slot = Mapper.Map<NetworkEquipmentSlot>(slotChanged.Slot);
            GameInteraction.UpdateEquipmentSlot(slot);
        }

        private void OnNotifyDropItem(NotifyDropItem item)
        {
            Logger.LogInformation("Received {messageType}. OwnerId={ownerId}, ItemId={itemId}, ItemName={itemName}", nameof(NotifyDropItem), item.Drop.OwnerEntityId, item.Drop.Item.UniqueId, item.Drop.Item.Name);

            var dropItem = Mapper.Map<NetworkDropItem>(item.Drop);
            GameInteraction.DropItem(dropItem);
        }

        private void OnNotifyContainerLooted(NotifyContainerLooted looted)
        {
            Logger.LogInformation("Received {messageType}. ContainerId={containerId}, ContainerPosition={containerPosition}, ItemsCount={itemsCount}, Items={itemsIds}",
               nameof(NotifyContainerLooted), looted.Container.Id, looted.Container.Position, looted.Container.Items.Count, looted.Container.Items.Select(i => i.UniqueId));

            var container = Mapper.Map<NetworkLootContainer>(looted.Container);
            GameInteraction.CollectContainerLoot(container);
        }

        private void OnNotifyToggleActivatableAbility(NotifyToggleActivatableAbility toggle)
        {
            Logger.LogInformation("Received {messageType}. AbilityId={abilityId}, IsActive={isActive}", nameof(NotifyToggleActivatableAbility), toggle.Ability.Id, toggle.Ability.IsActive);
            var ability = Mapper.Map<NetworkActivatableAbility>(toggle.Ability);
            GameInteraction.ToggleActivatableAbility(ability);
        }

        private void OnNotifyAbilityUsed(NotifyAbilityUse used)
        {
            Logger.LogInformation("Received {messageType}. AbilityId={abilityId}", nameof(NotifyAbilityUse), used.Ability.Id);
            var ability = Mapper.Map<NetworkAbility>(used.Ability);
            GameInteraction.UseAbility(ability);
        }

        private async void OnNotifyCombatTurnSynchronizationRequired(NotifyCombatTurnSynchronizationRequired required)
        {
            try
            {
                Logger.LogInformation("Received {messageType}. Units={unitsCount}", nameof(NotifyCombatTurnSynchronizationRequired), required.Units.Count);

                await SynchronizeUnitsAsync(required.Units);

                Logger.LogInformation("Units have been synchronized. Sending {messageType} confirmation", nameof(NotifyCombatTurnSynchronizationRequired));
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
            Logger.LogInformation("Received {messageType}. Round={round}, UnitId={unitId}", nameof(PlayerCombatTurnEnded), ended.Round, ended.UnitId);
            if (Game.Combat.Round == ended.Round && string.Equals(Game.Combat.Turn?.UnitId, ended.UnitId, StringComparison.OrdinalIgnoreCase))
            {
                EndLocalTurn();
            }
        }

        private void OnNotifyGroundClicked(NotifyGroundClicked clicked)
        {
            Logger.LogInformation("Received {messageType}. SelectedUnitId={selectedUnits}, WorldPosition={worldPosition}", nameof(NotifyGroundClicked), clicked.Click.SelectedUnits.Count, clicked.Click.WorldPosition);
            if (Game.Combat == null)
            {
                Logger.LogWarning("{messageType} is ignored out of combat", nameof(NotifyGroundClicked));
                return;
            }

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickGroundInCombat(click);
        }

        private void OnNotifyUnitClicked(NotifyUnitClicked clicked)
        {
            Logger.LogInformation("Received {messageType}. TargetUnitId={targetUnitId}, SelectedUnits={selectedUnits}", nameof(NotifyUnitClicked), clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickUnit(click);
        }

        private void OnNotifyMapObjectClicked(NotifyMapObjectClicked clicked)
        {
            Logger.LogInformation("Received {messageType}.TargetUnitId={targetUnitId}, SelectedUnits={selectedUnits}", nameof(NotifyMapObjectClicked), clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickMapObject(click);
        }

        private async void OnRollRequest(DiceRollValueRequest request)
        {
            Logger.LogInformation("Received {messageType}. RollId={rollId}", nameof(DiceRollValueRequest), request.RollId);
            // only host could ask for a roll since there is no direct connection between clients
            await SendLocalRollAsync(LocalHostPlayerId, request);
        }

        private void OnNotifyCombatTurnStarted(NotifyCombatTurnStarted started)
        {
            Logger.LogInformation("Received {messageType}. Round={round}, UnitId={unitId}", nameof(NotifyCombatTurnStarted), started.Round, started.UnitId);
            if (Game.Combat?.Turn == null)
            {
                Logger.LogError("Trying to start not initialized turn. Round={round}, UnitId={unitId}", started.Round, started.UnitId);
                return;
            }

            if (!string.Equals(started.UnitId, Game.Combat.Turn.UnitId, StringComparison.OrdinalIgnoreCase))
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

        private async void OnNotifyCombatInitialized(NotifyCombatInitialized combatInitialized)
        {
            Logger.LogInformation("Received {messageType}. Units={unitsCount}", nameof(NotifyCombatInitialized), combatInitialized.Units.Count);

            if (Game.Combat == null)
            {
                Logger.LogWarning("Combat has not been started on client yet. Waiting until start");
                while (Game.Combat == null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }


            await SynchronizeUnitsAsync(combatInitialized.Units);

            Game.Combat.IsInitialized = true;
            Game.Combat.InitialCombatOrder = [.. combatInitialized.UnitsCombatOrder];

            Logger.LogInformation("Sending {messageType}", nameof(ClientCombatInitialized));
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
            Logger.LogInformation("Received {messageType}.  DialogueName={dialogName},  TargetUnitId={targetId}, InitiatorUnitId={initiatorId}", nameof(NotifyDialogStarted), started.DialogName, started.TargetUnitId, started.InitiatorUnitId);
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
            Logger.LogInformation("Received {messageType}. DialogName={dialogName}, CueName={cueName}, AnswerName={answerName}", nameof(NotifyDialogCueAnswerSelected), selected.DialogName, selected.CueName, selected.AnswerName);
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

            Game.Dialog.AnswerSuggestions.Clear();
            GameInteraction.SelectDialogAnswer(selected.DialogName, selected.CueName, selected.AnswerName, selected.ManualUnitSelectionId);
        }

        private void OnNotifyDialogCueAnswerSuggested(NotifyDialogCueAnswerSuggested suggested)
        {
            Logger.LogInformation("Received {messageType}. DialogName={dialogName}, CueName={cueName}, Suggestions={suggestionsCount}", nameof(NotifyDialogCueAnswerSuggested), suggested.DialogName, suggested.CueName, suggested.Suggestions.Count);

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
            Logger.LogInformation("Received {messageType}. AreaExitId={areaExitId}", nameof(NotifyPartyLeaveArea), area.AreaExitId);
            GameInteraction.LeaveArea(area.AreaExitId);
        }

        private void OnNotifyGamePauseChanged(NotifyGamePauseChanged changed)
        {
            Logger.LogInformation("Received {messageType}. Value={value}", nameof(NotifyGamePauseChanged), changed.IsPaused);
            GameInteraction.Pause(changed.IsPaused);
        }

        private void OnNotifyCharacterMove(NotifyCharacterMove move)
        {
            Logger.LogInformation("Received {messageType}. UnitId={UnitId}, Destination={destination}", nameof(NotifyCharacterMove), move.UnitId, move.Destination);

            var destination = Mapper.Map<NetworkVector3>(move.Destination);
            GameInteraction.MoveNonCombatCharacter(move.UnitId, destination, move.Delay, move.Orientation);
        }

        private void OnNotifyGameStarted(NotifyGameStarted started)
        {
            Logger.LogInformation("Received {messageType}", nameof(NotifyGameStarted));
            if (string.IsNullOrEmpty(Game.SaveFilePath))
            {
                Logger.LogCritical("Trying to start a game with missing save file path");
                return;
            }

            InvokeOnStartGame();
        }

        private void OnNotifyCharactersOwnerChanged(NotifyCharactersOwnerChanged changed)
        {
            Logger.LogInformation("Received {messageType}. OwnersCount={ownersCount}", nameof(NotifyCharactersOwnerChanged), changed.Owners.Count);
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
            Logger.LogInformation("Received {messageType}. Status={newGameStatus}", nameof(NotifyGameStarted), changed.Stage);
            Game.Stage = (NetworkGameStage)Enum.Parse(typeof(NetworkGameStage), changed.Stage, true);
        }

        private void OnNotifySaveGameAssigned(NotifySaveGameAssigned assigned)
        {
            Logger.LogInformation("Received {messageType}. GameStatus={status}, Size={contentSize}, IsForceLoad={isForceLoad}", nameof(NotifySaveGameAssigned), Game.Stage, assigned.Content.Length, assigned.IsForceLoad);

            Game.SaveFilePath = StoreSaveFile(assigned.Content);
            Game.Id = assigned.GameId;

            if (assigned.IsForceLoad)
            {
                ForceLoadGame();
                return;
            }

            Logger.LogInformation("Game is ready to be started. SavePath={savePath}", Game.SaveFilePath);
            _networkServerClient.Send(new PlayerSaveGameSyncChanged { IsSynced = true });
        }

        private void OnPlayerReadyStatusChanged(PlayerReadyStatusChanged readyStatusChanged)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, IsReady={isReady}", nameof(PlayerReadyStatusChanged), readyStatusChanged.PlayerId, readyStatusChanged.IsReady);

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
            Logger.LogInformation("Received {messageType}. Portraits={portraits}", nameof(NotifyGameCharactersChanged), string.Join(";", changed.Characters.Select(c => c.Portrait)));
            Game.Characters.Clear();
            Game.Characters.AddRange(changed.Characters.Select(c => new NetworkCharacterOwnership { Name = c.Name, Portrait = c.Portrait }));
            OnGameCharactersChanged?.Invoke(Game.Characters);
        }

        private void OnNotifyPlayersChanged(NotifyPlayersChanged changed)
        {
            Logger.LogInformation("Received {messageType}. PlayersCount={playersCount}", nameof(NotifyPlayersChanged), changed.Players.Count);
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
            Logger.LogInformation("Received {messageType}. ClientPlayerId={clientPlayerId}", nameof(PlayerNameRequest), request.ClientPlayerId);
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
