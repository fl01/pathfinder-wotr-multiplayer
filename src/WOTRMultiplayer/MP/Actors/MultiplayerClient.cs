using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AutoMapper;
using Kingmaker.Controllers.Rest;
using Kingmaker.GameModes;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.MP.Actors;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Settings;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.Networking.Messages.Requests;
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
            IValueGenerator valueGenerator,
            IMapper mapper)
            : base(logger,
                  mapper,
                  multiplayerSettingsProvider,
                  gameInteractionService,
                  diceRollStorage,
                  fileSystemService,
                  valueGenerator)
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
                Destination = Mapper.Map<Networking.Messages.Contracts.NetworkVector3>(destination),
                Delay = delay,
                Orientation = orientation
            };
            _networkServerClient.Send(message);
        }

        public override void OnAreaScenesLoaded()
        {
            base.OnAreaScenesLoaded();
            _networkServerClient.Send(new ClientAreaLoaded());
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
                Game.Dialog.IsSelectingAnswer = false;
                return true;
            }

            var message = new DialogCueAnswerSuggested { DialogName = dialogName, CueName = cueName, AnswerName = answerName };
            Logger.LogInformation("Sending dialog answer suggestion. DialogName={dialogName}, CueName={cueName}, AnswerName={answerName}", message.DialogName, message.CueName, message.AnswerName);
            _networkServerClient.Send(message);
            Game.Dialog.IsSelectingAnswer = true;
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
            var message = new ClientDialogStartRequested
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

        /// <summary>
        /// 35 - UnitCombatPrepareController
        /// </summary>
        /// <returns></returns>
        public bool CanInitializeCombat()
        {
            return Game.Combat != null && Game.Combat.IsInitialized;
        }

        /// <summary>
        /// 12 - CombatController
        /// </summary>
        /// <returns></returns>
        public bool CanContinueCombat()
        {
            if (Game.Combat == null)
            {
                return false;
            }

            return Game.Combat.IsInitialized;
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

        public bool OnShowRestView(RestPhase phase)
        {
            Logger.LogInformation("Showing rest view. Phase={phase}", phase);
            if (phase == RestPhase.ShowingResults)
            {
                var message = new ClientRestEnded();
                Send(message);
            }

            return false;
        }
        public void OnBeforeTryRollRandomEncounter()
        {
            try
            {
                Logger.LogInformation("Retrieving random encounter context");

                // big timeout to make sure host is finished with banter. TODO: either deny or sync skipping banters
                var message = new RandomEncounterContextRequest { Timeout = TimeSpan.FromSeconds(45) };
                var response = _networkServerClient.SendAndWaitFor<RandomEncounterContextResponse>(message);

                if (response?.Encounter == null)
                {
                    Logger.LogError("Host return null encounter");
                    return;
                }

                var context = new NetworkRandomEncounterContext
                {
                    PreRecorded = Mapper.Map<NetworkRandomEncounter>(response.Encounter)
                };

                Logger.LogInformation("Random encounter context has been retrieved. Data={encounter}", context.PreRecorded);

                GameInteraction.SetRandomEncounterContext(context);

                if (context.PreRecorded.RandomUnitSeed.HasValue)
                {
                    EnsureForcePaused(UIStringConsts.GameNotifications.ForcedPauseReasons.RandomEncounterLoading);
                    GameInteraction.Pause(true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to retreive random encounter context");
                throw;
            }
        }

        public NetworkAIAction OnAfterAISelectedAction(NetworkAIAction networkAIAction)
        {
            try
            {
                if (!SettingsProvider.Settings.EnableCombatAIActionsSync || string.IsNullOrEmpty(networkAIAction.ActionBlueprintId))
                {
                    return null;
                }

                var message = new AIActionRequest
                {
                    Timeout = TimeSpan.FromSeconds(3),
                    UnitId = networkAIAction.UnitId,
                    ActionIndex = Game.Combat.AIActions.Count
                };

                Logger.LogInformation("Retrieving AI action. UnitId={unitID}, ActionIndex={inadex}", networkAIAction.UnitId, message.ActionIndex);

                var response = _networkServerClient.SendAndWaitFor<AIActionResponse>(message);

                if (response?.Action == null)
                {
                    Logger.LogWarning("Host has no next action for current unit. UnitId={unitId}", networkAIAction.UnitId);
                    return null;
                }

                var action = Mapper.Map<NetworkAIAction>(response.Action);

                if (string.Equals(action.ActionBlueprintId, networkAIAction.ActionBlueprintId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(action.TargetId, networkAIAction.TargetId, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInformation("Host AI action is the same, nothing to do here. UnitId={unitID}, ActionBlueprintId={actionId}, TargetUnitId={targetId}", networkAIAction.UnitId, networkAIAction.ActionBlueprintId, networkAIAction.TargetId);
                    Game.Combat.AIActions.Add(networkAIAction);
                    return null;
                }

                Game.Combat.AIActions.Add(action);
                return action;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to retreive AI action. UnitId={unitId}", networkAIAction.UnitId);
                throw;
            }
        }

        protected override bool OnStartGameModeInternal(GameModeType type)
        {
            var playerId = GetLocalPlayerId();
            var isFirstTime = RegisterGameMode(type, playerId);
            if (!isFirstTime)
            {
                return true;
            }

            var message = new ClientGameModeTypeStarted { TypeId = type.Index };
            Logger.LogInformation("Sending {messageType}. TypeId={typeId}", nameof(ClientGameModeTypeStarted), message.TypeId);
            Send(message);
            return true;
        }

        protected override bool OnStopGameModeInternal(GameModeType type)
        {
            var playerId = GetLocalPlayerId();
            var isFirstTime = UnregisterGameMode(type, playerId);
            if (isFirstTime)
            {
                var message = new ClientGameModeTypeEnded { TypeId = type.Index };
                Logger.LogInformation("Sending {messageType}. TypeId={typeId}", nameof(ClientGameModeTypeEnded), message.TypeId);
                Send(message);

                if (type == GameModeType.Rest && Game.ForcedPause != null)
                {
                    GameInteraction.Pause(true);
                }
            }

            return true;
        }

        protected override DiceRollValueResponse RetrieveRoll(DiceRollValueRequest request)
        {
            return _networkServerClient.SendAndWaitFor<DiceRollValueResponse>(request);
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
            var message = new PlayerCombatTurnEnded { UnitId = Game.Combat.Turn.UnitId };
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
                .Register<DiceRollValueRequest>(OnDiceRollValueRequest)

                .Register<NotifyPlayerDisconnected>(OnNotifyPlayerDisconnected)
                .Register<GameServerConnectionSucceeded>(OnGameServerConnectionSucceeded)
                .Register<PlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
                .Register<NotifyPlayersChanged>(OnNotifyPlayersChanged)
                .Register<NotifyGameCharactersChanged>(OnNotifyGameCharactersChanged)
                .Register<NotifySaveGameAssigned>(OnNotifySaveGameAssigned)
                .Register<NotifyGameStageChanged>(OnNotifyGameStageChanged)
                .Register<NotifyCharactersOwnerChanged>(OnNotifyCharactersOwnerChanged)
                .Register<NotifyGameStarted>(OnNotifyGameStarted)
                .Register<NotifyCharacterMove>(OnNotifyCharacterMove)
                .Register<NotifyForcedPauseEnded>(OnNotifyForcedPauseEnded)
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
                .Register<NotifyInspectionKnowledgeCheckRolled>(OnNotifyInspectionKnowledgeCheckRolled)
                .Register<NotifySpawnCampPlace>(OnNotifySpawnCampPlace)
                .Register<NotifyCampingUseHealingSpellsChanged>(OnNotifyCampingUseHealingSpellsChanged)
                .Register<NotifyCampingStateChanged>(OnNotifyCampingStateChanged)
                .Register<NotifyCampingUnitsRoleChanged>(OnNotifyCampingUnitsRoleChanged)
                .Register<NotifyRestStarted>(OnNotifyRestStarted)
                .Register<NotifyRestBanterInterrupted>(OnNotifyRestBanterInterrupted)
                .Register<NotifyInvalidCombatTurnStarted>(OnNotifyInvalidCombatTurnStarted)
                ;

            _networkServerClient.OnError = OnNetworkClientError;
            _networkServerClient.OnConnected = OnNetworkClientConnected;
        }

        private void OnNotifyInvalidCombatTurnStarted(NotifyInvalidCombatTurnStarted started)
        {
            Logger.LogInformation("Received {messageType}. UnitId={unitId}", nameof(NotifyInvalidCombatTurnStarted), started.UnitId);
            GameInteraction.AddCombatText(UIStringConsts.GameNotifications.CombatLog.ClientIsFixingCombaTurnOrderDesync);
            Game.Combat.Turn = null;
            GameInteraction.StartTurnBasedCombatTurnAsAnotherUnit(started.UnitId);
        }

        private void OnNotifyRestBanterInterrupted(NotifyRestBanterInterrupted interrupted)
        {
            Logger.LogInformation("Received {messageType}. SpeakerUnitId={speakerUnitId}, Key={key}", nameof(NotifyRestBanterInterrupted), interrupted.Banter.SpeakerUnitId, interrupted.Banter.Key);
            var banter = Mapper.Map<NetworkRestBanter>(interrupted.Banter);
            GameInteraction.TryInterruptRestBanter(banter);
        }

        private void OnNotifyRestStarted(NotifyRestStarted started)
        {
            Logger.LogInformation("Received {messageType}", nameof(NotifyRestStarted));
            GameInteraction.StartRest();
        }

        private void OnNotifyCampingUnitsRoleChanged(NotifyCampingUnitsRoleChanged changed)
        {
            var rolesData = string.Join(" ,", changed.Roles.Select(r => $"[RoleType={r.RoleType} PrimaryUnit={r.PrimaryUnitId} SecondaryUnit={r.SecondaryUnitId}]"));
            Logger.LogInformation("Received {messageType}. RolesCount={rolesCount}, RolesData={rolesData}", nameof(NotifyCampingUnitsRoleChanged), changed.Roles.Count, rolesData);

            var roles = Mapper.Map<List<NetworkCampingRole>>(changed.Roles);
            GameInteraction.SetCampingRoles(roles);
        }

        private void OnNotifyCampingStateChanged(NotifyCampingStateChanged changed)
        {
            Logger.LogInformation("Received {messageType}. CookingBlueprintRecipeId={cookingId}, PotionBlueprintRecipeId={potionId}, ScrollBlueprintRecipeId={ScrollId}, IterationsCount={iterations}, AutotuneIterations={autotuneIterations}", nameof(NotifyCampingStateChanged),
                changed.State.CookingBlueprintRecipeId, changed.State.PotionBlueprintRecipeId, changed.State.ScrollBlueprintRecipeId, changed.State.IterationsCount, changed.State.AutotuneIterationsStatus);

            var state = Mapper.Map<NetworkCampingState>(changed.State);
            GameInteraction.SetCampingState(state);
        }

        private void OnNotifyCampingUseHealingSpellsChanged(NotifyCampingUseHealingSpellsChanged changed)
        {
            Logger.LogInformation("Received {messageType}. IsOn={isOn}", nameof(NotifyCampingUseHealingSpellsChanged), changed.IsOn);
            GameInteraction.SetCampingUseHealingSpells(changed.IsOn);
        }

        private void OnNotifySpawnCampPlace(NotifySpawnCampPlace place)
        {
            Logger.LogInformation("Received {messageType}. Position={position}", nameof(NotifySpawnCampPlace), place.Position);
            var position = Mapper.Map<NetworkVector3>(place.Position);
            GameInteraction.SpawnCampPlace(position);
        }

        private void OnNotifyPlayerDisconnected(NotifyPlayerDisconnected disconnected)
        {
            Logger.LogInformation("Received {messageType}. UnitId={unitID}, MapObjectId={round}", nameof(NotifyPlayerDisconnected), disconnected.PlayerId);
            var player = CleanupPlayer(disconnected.PlayerId);
            ShowPlayerDisconnectedMessage(player);
        }

        private void OnNotifyInspectionKnowledgeCheckRolled(NotifyInspectionKnowledgeCheckRolled rolled)
        {
            Logger.LogInformation("Received {messageType}. TargetUnitId={targetUnitId}, InitiatorUnitId={initiatorId}, StatType={statType}, DC={dc}", nameof(NotifyInspectionKnowledgeCheckRolled), rolled.Check.TargetUnitId, rolled.Check.InitiatorUnitId, rolled.Check.StatType, rolled.Check.DC);
            var check = Mapper.Map<NetworkInspectionKnowledgeCheck>(rolled.Check);
            GameInteraction.ApplyInspectionKnowledgeCheck(check);
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
            Logger.LogInformation("Received {messageType}. SlotType={slotType}, SlotIndex={slotIndex}, ItemId={itemId}, OwnerId={ownerId}", nameof(NotifyEquipmentSlotChanged), slotChanged.Slot.Position.Type, slotChanged.Slot.Position.Index, slotChanged.Slot.Item?.UniqueId, slotChanged.Slot.OwnerId);
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
                Logger.LogInformation("Received {messageType}. Units={unitsCount}, UnitTurn={unitTurn}", nameof(NotifyCombatTurnSynchronizationRequired), required.Units.Count, required.UnitId);

                var unitId = Game.Combat.Turn.UnitId;
                if (!string.Equals(unitId, required.UnitId, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("Synchronization request contains mismatched unit. LocalUnitId={localUnitId}, RemoteRound={remoteRound}, RemoteUnitId={remoteUnitId}", unitId, required.UnitId);
                    return;
                }

                await SynchronizeUnitsAsync(required.Units);

                var message = new ClientCombatTurnSynchronized { UnitId = unitId };
                Logger.LogInformation("Units have been synchronized. Sending {messageType} confirmation. UnitId={unitId}", nameof(NotifyCombatTurnSynchronizationRequired), unitId);
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
            Logger.LogInformation("Received {messageType}. UnitId={unitId}", nameof(PlayerCombatTurnEnded), ended.UnitId);
            if (!string.Equals(Game.Combat.Turn?.UnitId, ended.UnitId, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning("Another player ended different turn. LocalUnitId={localUnitId}, RemoteUnitId={remoteUnitId}", Game.Combat.Turn?.UnitId, ended.UnitId);
                return;
            }

            EndLocalTurn();
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

        private async void OnDiceRollValueRequest(DiceRollValueRequest request)
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
            GameInteraction.StartTurnBasedCombatTurn(Game.Combat.Turn.UnitId);
        }

        private async void OnNotifyCombatInitialized(NotifyCombatInitialized combatInitialized)
        {
            Logger.LogInformation("Received {messageType}. Units={unitsCount}", nameof(NotifyCombatInitialized), combatInitialized.Units.Count);

            if (Game.Combat == null)
            {
                Logger.LogWarning("Combat has not been started on client yet. Waiting until start");
                while (Game.Combat == null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }
            }

            await SynchronizeUnitsAsync(combatInitialized.Units);

            Logger.LogInformation("Sending {messageType}", nameof(ClientCombatInitialized));
            var message = new ClientCombatInitialized();
            _networkServerClient.Send(message);

            Game.Combat.IsInitialized = true;
        }

        private Task SynchronizeUnitsAsync(List<Networking.Messages.Contracts.NetworkUnit> units)
        {
            var unitsToSync = Mapper.Map<List<NetworkUnit>>(units);

            return GameInteraction.UpdateUnitsAsync(unitsToSync);
        }

        private async void OnNotifyDialogStarted(NotifyDialogStarted started)
        {
            Logger.LogInformation("Received {messageType}.  DialogueName={dialogName},  TargetUnitId={targetId}, InitiatorUnitId={initiatorId}", nameof(NotifyDialogStarted), started.DialogName, started.TargetUnitId, started.InitiatorUnitId);

            if (Game.Dialog != null && Game.Dialog.IsSelectingAnswer)
            {
                Logger.LogWarning("Waiting until client finished processing previos answer");
                while (Game.Dialog.IsSelectingAnswer)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }
            }

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

        private void OnNotifyForcedPauseEnded(NotifyForcedPauseEnded changed)
        {
            Logger.LogInformation("Received {messageType}", nameof(NotifyForcedPauseEnded));
            Game.ForcedPause = null;
            GameInteraction.Pause(false);
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

        private void OnGameServerConnectionSucceeded(GameServerConnectionSucceeded succeeded)
        {
            Logger.LogInformation("Received {messageType}. ClientPlayerId={clientPlayerId}, RestBanterSeed={restBanterSeed}", nameof(GameServerConnectionSucceeded), succeeded.ClientPlayerId, succeeded.RestBanterSeed);

            Game.LocalPlayerId = succeeded.ClientPlayerId;
            Game.RestBanterSeed = succeeded.RestBanterSeed;

            var settings = Mapper.Map<NetworkGameSettings>(succeeded.GameSettings);
            GameInteraction.ApplyGameSettings(settings);

            var message = new ClientGameServerConnectionConfirmed() { PlayerName = SettingsProvider.Settings.PlayerName };
            Logger.LogInformation("Sending {messageType}. PlayerName={playerName}", nameof(ClientGameServerConnectionConfirmed), message.PlayerName);
            _networkServerClient.Send(message);
        }
    }
}
