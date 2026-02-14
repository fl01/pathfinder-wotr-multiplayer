using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using AutoMapper;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.GameInteraction.CombatLog;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Area;
using WOTRMultiplayer.Entities.AreaEffects;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.NewGame;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Entities.Units;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.Networking.Messages.Requests;
using WOTRMultiplayer.Services.GameInteraction.Contexts;

namespace WOTRMultiplayer.Services
{
    public class MultiplayerClient : MultiplayerActorBase, IMultiplayerClient
    {
        private readonly IIPEndPointParser _ipEndPointParser;
        private readonly INetworkClient _networkClient;

        public Action OnNetworkError { get; set; }

        public Action<NetworkGameConnectivity> OnConnected { get; set; }

        public Action<NetworkCharacter> OnCharacterOwnerChanged { get; set; }

        public bool IsActive => _networkClient.IsActive;

        public bool IsConnecting => _networkClient.IsConnecting;

        private NetworkLobbyStage Status => Game?.Stage ?? NetworkLobbyStage.None;

        public bool IsInLobby => IsActive && Status == NetworkLobbyStage.Lobby;

        protected override bool HasControlOverUI => false;

        public MultiplayerClient(
            ILogger<MultiplayerClient> logger,
            IGameInteractionService gameInteractionService,
            ILevelingInteractionService levelingInteractionService,
            IPlayerNotificationService playerNotificationService,
            IDialogInteractionService dialogInteractionService,
            IGlobalMapInteractionService globalMapInteractionService,
            IPingInteractionService pingInteractionService,
            ICombatInteractionService combatInteractionService,
            IIPEndPointParser ipEndPointParser,
            IMultiplayerSettingsService multiplayerSettingsProvider,
            IFileSystemService fileSystemService,
            INetworkClient networkClient,
            IDiceRollStorage diceRollStorage,
            IValueGenerator valueGenerator,
            IMapper mapper)
            : base(logger,
                  mapper,
                  multiplayerSettingsProvider,
                  gameInteractionService,
                  levelingInteractionService,
                  playerNotificationService,
                  dialogInteractionService,
                  globalMapInteractionService,
                  pingInteractionService,
                  combatInteractionService,
                  diceRollStorage,
                  fileSystemService,
                  valueGenerator,
                  networkClient)
        {
            _ipEndPointParser = ipEndPointParser;
            _networkClient = networkClient;
        }

        public AddressParseResult Connect(string address)
        {
            var endpoint = _ipEndPointParser.Parse(address);
            if (endpoint == null)
            {
                return AddressParseResult.Error(WellKnownKeys.MultiplayerClient.Errors.InvalidAddress.Key);
            }

            if (endpoint.Port == 0)
            {
                return AddressParseResult.Error(WellKnownKeys.MultiplayerClient.Errors.InvalidPort.Key);
            }

            SetupNetworkMessageHandlers();

            var settings = SettingsService.GetSettings();
            _networkClient.ConnectAsync(endpoint.Address.ToString(), endpoint.Port, settings.NetworkAwaiterTimeout);

            return AddressParseResult.Ok();
        }

        public void Reset()
        {
            Logger.LogInformation("Resetting");

            Game = null;

            _networkClient?.Reset();
        }

        public override void OnAreaLoadingComplete()
        {
            base.OnAreaLoadingComplete();

            var message = new ClientAreaLoaded();
            Logger.LogInformation("Sending {MessageType}", nameof(ClientAreaLoaded));
            Send(message);
        }

        public void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer)
        {
            Logger.LogInformation("Showing dialog Cue. DialogName={DialogName}, CueName={CueName}, HasSystemAnswer={HasSystemAnswer}", dialogName, cueName, hasSystemAnswer);
            if (hasSystemAnswer)
            {
                DialogInteraction.SetDialogContinueButtonState(false);
            }

            Game.Dialog.CurrentCueName = cueName;
            Game.Dialog.Answer = null;

            var message = new ClientDialogCueWitnessed { CueName = cueName, DialogName = dialogName };
            Logger.LogInformation("Sending {MessageType}. DialogName={DialogName}, CueName={CueName}", nameof(ClientDialogCueWitnessed), message.DialogName, message.CueName);
            Send(message);
        }

        public bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId)
        {
            Logger.LogInformation("Select Dialog Answer. DialogName={DialogName}, CueName={CueName}, Answer={Answer}, IsExitAnswer={IsExitAnswer}, ManualUnitSelectionId={ManualUnitSelectionId}", dialogName, cueName, answerName, isExitAnswer, manualUnitSelectionId);
            if (Game.Dialog == null)
            {
                Logger.LogError("Current dialog is null");
                return false;
            }

            if (!string.Equals(Game.Dialog.Name, dialogName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Game.Dialog.CurrentCueName, cueName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog answer mismatch. ExpectedDialogName={ExpectedDialogName}, ExpectedCueName={ExpectedCueName}, ActualDialogName={ActualDialogName}, ActualCueName={ActualCueName}", Game.Dialog.Name, Game.Dialog.CurrentCueName, dialogName, cueName);
                return false;
            }

            // answer could be set from host notifications only
            // so it means we have a response from host and shouldn't skip default game logic
            if (Game.Dialog.Answer != null && string.Equals(answerName, Game.Dialog.Answer.AnswerName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Proceeding with dialog answer without extra steps. DialogName={DialogName}, CueName={CueName}, AnswerName={AnswerName}", dialogName, cueName, answerName);
                Game.Dialog.IsSelectingAnswer = false;
                return true;
            }

            var message = new ClientDialogCueAnswerSuggested { DialogName = dialogName, CueName = cueName, AnswerName = answerName };
            Logger.LogInformation("Sending {MessageType}. DialogName={DialogName}, CueName={CueName}, AnswerName={AnswerName}", nameof(ClientDialogCueAnswerSuggested), message.DialogName, message.CueName, message.AnswerName);
            Send(message);
            Game.Dialog.IsSelectingAnswer = true;
            return false;
        }

        public bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
        {
            if (string.Equals(Game.Dialog?.Name, dialogName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Dialog has been initiated, proceeding with default game logic.  DialogName={DialogName}", dialogName);
                return true;
            }

            var message = new ClientDialogStartRequested
            {
                DialogName = dialogName,
                TargetUnitId = targetUnitId,
                InitiatorUnitId = initiatorUnitId,
                MapObjectId = mapObjectId,
                SpeakerKey = speakerKey
            };
            Logger.LogInformation("Sending {MessageType}. DialogName={DialogName}, TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}, MapObjectId={MapObjectId}, SpeakerKey={SpeakerKey}", nameof(ClientDialogStartRequested), message.DialogName, message.TargetUnitId, message.InitiatorUnitId, message.MapObjectId, message.SpeakerKey);
            Send(message);

            return false;
        }

        public bool CanInitializeCombat()
        {
            if (Game.Combat == null)
            {
                return false;
            }

            if (!Game.Combat.IsPrepared)
            {
                var units = CombatInteraction.GetUnitsInCombat();
                var message = new ClientCombatPreparationStarted
                {
                    Units = Mapper.Map<List<Networking.Messages.Contracts.NetworkUnit>>(units),
                };
                Logger.LogInformation("Sending {MessageType}. UnitsCount={UnitsCount}", nameof(ClientCombatPreparationStarted), message.Units.Count);
                Send(message);
                Game.Combat.IsPrepared = true;
            }

            var canInitializeCombat = Game.Combat != null && Game.Combat.IsInitialized;
            return canInitializeCombat;
        }

        public bool CanContinueCombat()
        {
            var canContinueCombat = Game.Combat != null && Game.Combat.IsInitialized;
            return canContinueCombat;
        }

        public bool IsDiceRollOwner()
        {
            return !IsRolledByHost() && IsRolledByLocalPlayer();
        }

        public void OnBeforeTryRollRestRandomEncounter()
        {
            try
            {
                Logger.LogInformation("Retrieving rest random encounter context. SleepPhase={SleepPhase}", Game.Rest.SleepPhase);

                var settings = SettingsService.GetSettings();
                var message = new RandomEncounterContextRequest { SleepPhase = Game.Rest.SleepPhase, Timeout = settings.RestEncounterSyncTimeout };
                var response = _networkClient.SendAndWaitForAsync<RandomEncounterContextResponse>(message).Result;

                if (response?.Encounter == null)
                {
                    Logger.LogError("Host return null encounter");
                    return;
                }

                var context = new NetworkRandomEncounterContext
                {
                    PreRecorded = Mapper.Map<NetworkRandomEncounter>(response.Encounter)
                };

                Logger.LogInformation("Rest random encounter context has been retrieved. SleepPhase={SleepPhase}, Data={Data}", Game.Rest.SleepPhase, context.PreRecorded);

                GameInteraction.SetRandomEncounterContext(context);

                if (context.PreRecorded.RandomUnitSeed.HasValue)
                {
                    EnsureForcePaused(NetworkForcedPauseReason.RestEncounterLoading);
                    GameInteraction.SetPause(true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to retrieve rest random encounter context");
                throw;
            }
        }

        public NetworkAIAction OnAfterAISelectedAction(NetworkAIAction networkAIAction)
        {
            try
            {
                var aiActions = GetAIActions();
                if (aiActions == null)
                {
                    return null;
                }

                var count = aiActions.Count(x => x.UnitId == networkAIAction.UnitId);
                var settings = SettingsService.GetSettings();
                var message = new AIActionRequest
                {
                    Timeout = settings.AISyncTimeout,
                    UnitId = networkAIAction.UnitId,
                    ActionIndex = count
                };

                Logger.LogInformation("Retrieving AI action. UnitId={UnitId}, ActionIndex={ActionIndex}, LocalAction={LocationAction}, LocalTarget={LocalTarget}", networkAIAction.UnitId, message.ActionIndex, networkAIAction.ActionBlueprintId, networkAIAction.TargetId);

                var response = _networkClient.SendAndWaitForAsync<AIActionResponse>(message).Result;

                if (response?.Action == null)
                {
                    Logger.LogWarning("Host has no next action for current unit. UnitId={UnitId}", networkAIAction.UnitId);
                    return null;
                }

                var action = Mapper.Map<NetworkAIAction>(response.Action);

                if (aiActions.Count > 0
                    && aiActions[aiActions.Count - 1].ActionBlueprintId == null
                    && aiActions[aiActions.Count - 1].UnitId == action.UnitId
                    && action.ActionBlueprintId == null)
                {
                    Logger.LogInformation("Duplicate AI action has been skipped. UnitId={UnitId}", action.UnitId);
                    return networkAIAction;
                }

                aiActions.Add(action);

                if (string.Equals(action.ActionBlueprintId, networkAIAction.ActionBlueprintId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(action.TargetId, networkAIAction.TargetId, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInformation("Host AI action is the same, nothing to do here. UnitId={UnitId}, ActionBlueprintId={ActionBlueprintId}, TargetUnitId={TargetUnitId}", networkAIAction.UnitId, networkAIAction.ActionBlueprintId, networkAIAction.TargetId);
                    return null;
                }

                return action;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to retrieve AI action. UnitId={UnitId}", networkAIAction.UnitId);
                throw;
            }
        }

        public bool OnRequestLevelingUI(string unitId, NetworkLevelingType networkCharGenScreenType)
        {
            if (Game.Leveling != null)
            {
                return true;
            }

            var message = new ClientCharacterLevelingRequested
            {
                UnitId = unitId,
                Type = networkCharGenScreenType.ToString()
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, CharGenScreenType={CharGenScreenType}", nameof(ClientCharacterLevelingRequested), message.UnitId, message.Type);
            Send(message);
            return false;
        }

        public void OnAutoPausedByTrapDetection()
        {
            EnsureForcePaused(NetworkForcedPauseReason.TrapDetected, removalDelay: null);
            var message = new ClientGameAutoPaused
            {
                Pause = Mapper.Map<Networking.Messages.Contracts.NetworkForcedPause>(Game.ForcedPause)
            };
            Logger.LogInformation("Sending {MessageType}. Reason={Reason}, RemovalDelay={RemovalDelay}", nameof(ClientGameAutoPaused), message.Pause.Reason, message.Pause.RemovalDelay);
            Send(message);
        }

        public bool OnClickGroupChangerUnit(string unitId)
        {
            // client is not allowed to move characters (no restrictions, just to avoid implementing extra synchronization)
            return false;
        }

        public bool OnAreaEffectTriggered(NetworkAreaEffect areaEffect)
        {
            if (Game.Combat == null)
            {
                return true;
            }

            if (Game.Combat.TriggeredAreaEffects.Remove(areaEffect))
            {
                Logger.LogWarning("Area effect trigger has been allowed by host. Id={Id}, Name={Name}", areaEffect.Id, areaEffect.Name);
                return true;
            }

            var canTrigger = Game.Combat.Turn != null;
            if (!canTrigger)
            {
                Logger.LogWarning("Area effect trigger has been denied. Id={Id}, Name={Name}", areaEffect.Id, areaEffect.Name);
            }

            return canTrigger;
        }

        public bool OnGlobalMapSelectLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            if (globalMapLocation == null)
            {
                return false;
            }

            var canSelectLocation = GlobalMapInteraction.IsAtLocation(globalMapLocation);
            return canSelectLocation;
        }

        public bool OnSpawnCampPlace(NetworkVector3 position)
        {
            PlayerNotification.ShowWarningNotification(WellKnownKeys.GameNotifications.Rest.NoCampingPermission.Key);
            return false;
        }

        public bool OnCreateAndEquipPolymorphInSlot(NetworkPolymorphicItem polymorphicItem)
        {
            var isInParty = IsControlledByPlayers(polymorphicItem.UnitId) || GameInteraction.IsUnitInParty(polymorphicItem.UnitId);
            if (!isInParty)
            {
                return true;
            }

            var message = new NotifyPolymorphicItemCreationRequested
            {
                PolymorphicItem = Mapper.Map<Networking.Messages.Contracts.NetworkPolymorphicItem>(polymorphicItem)
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, ItemName={ItemName}, SlotType={SlotType}", nameof(NotifyPolymorphicItemCreationRequested), message.PolymorphicItem.UnitId, message.PolymorphicItem.Item.Name, message.PolymorphicItem.Position.Type);
            Send(message);

            return false;
        }

        public bool OnTacticalCombatInitialization()
        {
            if (Game.ArmyCombat != null && !Game.ArmyCombat.IsInitialized)
            {
                Game.ArmyCombat.IsInitialized = true;
                var message = new NotifyTacticalCombatInitializationConfirmed
                {
                    PlayerId = Game.LocalPlayerId
                };
                Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyTacticalCombatInitializationConfirmed), message.PlayerId);
                Send(message);
                return true;
            }

            ResetPlayersTracker(Game.PlayersInGlobalMapCombatResults);
            Game.ArmyCombat = new NetworkArmyCombat() { IsInitialized = false };

            return false;
        }

        protected override DiceRollValueResponse RetrieveRoll(DiceRollValueRequest rollRequest)
        {
            return _networkClient.SendAndWaitForAsync<DiceRollValueResponse>(rollRequest).Result;
        }

        protected override void Send(object message)
        {
            _networkClient.Send(message);
        }

        protected override void Send(long playerId, object message)
        {
            Send(message);
        }

        protected override void OnLocalPlayerTurnStart()
        {
            var message = new ClientCombatTurnStarted
            {
                UnitId = Game.Combat.Turn.UnitId,
                Round = Game.Combat.Round
            };

            Send(message);
        }

        protected override bool OnToggleOffPause(out bool showReason)
        {
            showReason = false;

            var message = new ClientTogglePauseOff { PlayerId = Game.LocalPlayerId };
            Send(message);
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(ClientTogglePauseOff), message.PlayerId);
            return false;
        }

        protected override void SetupNetworkMessageHandlers()
        {
            _networkClient.OnError = OnNetworkClientError;
            _networkClient.OnConnected = OnNetworkClientConnected;

            base.SetupNetworkMessageHandlers();

            _networkClient
               // this is kind of special because requester is blocking the thread (most likely game main loop) until <see cref="DiceRollValueResponse"/> is received
               .On<DiceRollValueRequest>(OnDiceRollValueRequest)

               // lobby
               .On<NotifyLobbySaveGameChanged>(OnNotifyLobbySaveGameChanged)
               .On<GameServerConnectionSucceeded>(OnGameServerConnectionSucceeded)
               .On<NotifyLobbyPlayersChanged>(OnNotifyLobbyPlayersChanged)
               .On<NotifyLobbyCharactersChanged>(OnNotifyLobbyCharactersChanged)
               .On<NotifyCharacterOwnerChanged>(OnNotifyCharacterOwnerChanged)
               .On<NotifyGameStarted>(OnNotifyGameStarted)
               .On<NotifyLobbySyncStatusChanged>(OnNotifyLobbySyncStatusChanged)
               .On<NotifyNewGameDifficultyChanged>(OnNotifyNewGameDifficultyChanged)

               // new game sequence
               .On<NotifyNewGameSequencePhaseChanged>(OnNotifyNewGameSequencePhaseChanged)
               .On<NotifyNewGameSequenceLevelingStarted>(OnNotifyNewGameSequenceLevelingStarted)
               .On<NotifyNewGameSequenceTerminated>(OnNotifyNewGameSequenceTerminated)

               // pausing
               .On<NotifyGamePauseEnded>(OnNotifyGamePauseEnded)

               // area transitioning
               .On<NotifyPartyAreaTransitioned>(OnNotifyPartyAreaTransitioned)

               // leveling
               .On<NotifyLevelingStarted>(OnNotifyLevelingStarted)

               // character selection window
               .On<NotifyCharacterSelectionToggleChanged>(OnNotifyCharacterSelectionToggleChanged)
               .On<NotifyCharacterSelectionWindowAccepted>(OnNotifyCharacterSelectionWindowAccepted)
               .On<NotifyCharacterSelectionWindowClosed>(OnNotifyCharacterSelectionWindowClosed)

               // rest
               .On<NotifyRestWindowClosed>(OnNotifyRestWindowClosed)
               .On<NotifyRestStarted>(OnNotifyRestStarted)
               .On<NotifySpawnCampPlace>(OnNotifySpawnCampPlace)
               .On<NotifyCampingUseHealingSpellsChanged>(OnNotifyCampingUseHealingSpellsChanged)
               .On<NotifyCampingStateChanged>(OnNotifyCampingStateChanged)
               .On<NotifyCampingUnitsRoleChanged>(OnNotifyCampingUnitsRoleChanged)

               // combat
               .On<NotifyCombatPreparationRequired>(OnNotifyCombatPreparationRequired)
               .On<NotifyCombatInitializationRequired>(OnNotifyCombatInitializationRequired)
               .On<NotifyCombatInitializationCompleted>(OnNotifyCombatInitializationCompleted)
               .On<NotifyInvalidCombatTurnStarted>(OnNotifyInvalidCombatTurnStarted)
               .On<NotifyCombatTurnSynchronizationRequired>(OnNotifyCombatTurnSynchronizationRequired)
               .On<NotifyCombatTurnStarted>(OnNotifyCombatTurnStarted)

               // global map & crusade combat
               .On<NotifyGlobalMapRestOpened>(OnNotifyGlobalMapRestOpened)
               .On<NotifyGlobalMapGroupChangerOpened>(OnNotifyGlobalMapGroupChangerOpened)
               .On<NotifyGlobalMapTravelStarted>(OnNotifyGlobalMapTravelStarted)
               .On<NotifyGlobalMapTravelStopped>(OnNotifyGlobalMapTravelStopped)
               .On<NotifyGlobalMapTravelContinued>(OnNotifyGlobalMapTravelContinued)
               .On<NotifyGlobalMapCommonPopupAccepted>(OnNotifyGlobalMapIngredientCollectionAccepted)
               .On<NotifyGlobalMapEncounterAccepted>(OnNotifyGlobalMapEncounterAccepted)
               .On<NotifyGlobalMapEncounterAvoided>(OnNotifyGlobalMapEncounterAvoided)
               .On<NotifyGlobalMapEncounterRolled>(OnNotifyGlobalMapEncounterRolled)
               .On<NotifyGlobalMapLocationMessageClosed>(OnNotifyGlobalMapLocationMessageClosed)
               .On<NotifyGlobalMapLocationMessageAccepted>(OnNotifyGlobalMapLocationMessageAccepted)
               .On<NotifyGlobalMapCommonPopupDeclined>(OnNotifyGlobalMapCommonPopupDeclined)
               .On<NotifyGlobalMapDaySkipped>(OnNotifyGlobalMapDaySkipped)
               .On<NotifyGlobalMapTravelerModeChanged>(OnNotifyGlobalMapTravelerModeChanged)
               .On<NotifyGlobalMapSelectedArmyChanged>(OnNotifyGlobalMapSelectedArmyChanged)
               .On<NotifyGlobalMapAutoCrusadeCombatChanged>(OnNotifyGlobalMapAutoCrusadeCombatChanged)
               .On<NotifyGlobalMapCombatResultsClosed>(OnNotifyGlobalMapCombatResultsClosed)
               .On<NotifyCrusadeArmyBattleResultsManualCombatStarted>(OnNotifyCrusadeArmyBattleResultsManualCombatStarted)
               .On<NotifyCrusadeArmyBattleResultsClosed>(OnNotifyCrusadeArmyBattleResultsClosed)
               .On<NotifyTacticalCombatInitialized>(OnNotifyTacticalCombatInitialized)
               .On<NotifyTacticalUnitAttackCommandExecuted>(OnNotifyTacticalUnitAttackCommandExecuted)
               .On<NotifyTacticalUnitUseAbilityCommandExecuted>(OnNotifyTacticalUnitUseAbilityCommandExecuted)
               .On<NotifyTacticalUnitMoveToCommandExecuted>(OnNotifyTacticalUnitMoveToCommandExecuted)
               .On<NotifyTacticalCombatTurnPostponed>(OnNotifyTacticalCombatTurnPostponed)
               .On<NotifyTacticalCombatTotalDefenseUsed>(OnNotifyTacticalCombatTotalDefenseUsed)
               .On<NotifyTacticalCombatRetreated>(OnNotifyTacticalCombatRetreated)
               .On<NotifyGlobalMapCrusadeArmySquadSplitted>(OnNotifyGlobalMapCrusadeArmySquadSplitted)
               .On<NotifyGlobalMapCrusadeArmySquadsMerged>(OnNotifyGlobalMapCrusadeArmySquadsMerged)
               .On<NotifyGlobalMapCrusadeArmySquadsSwitched>(OnNotifyGlobalMapCrusadeArmySquadsSwitched)
               .On<NotifyGlobalMapCrusadeArmySquadSplitRequested>(OnNotifyGlobalMapCrusadeArmySquadSplitRequested)
               .On<NotifyGlobalMapCrusadeArmyMergedInOne>(OnNotifyGlobalMapCrusadeArmyMergedInOne)
               .On<NotifyGlobalMapCrusadeArmySquadDismissed>(OnNotifyGlobalMapCrusadeArmySquadDismissed)
               .On<NotifyGlobalMapCrusadeArmyInfoClosed>(OnNotifyGlobalMapCrusadeArmyInfoClosed)
               .On<NotifyGlobalMapCrusadeArmyInfoShown>(OnNotifyGlobalMapCrusadeArmyInfoShown)
               .On<NotifyGlobalMapCrusadeArmySquadsMovedToMainArmy>(OnNotifyGlobalMapCrusadeArmySquadsMovedToMainArmy)
               .On<NotifyGlobalMapCrusadeArmySquadsMovedToSecondArmy>(OnNotifyGlobalMapCrusadeArmySquadsMovedToSecondArmy)
               .On<NotifyGlobalMapCrusadeArmyInfoNextMergeArmySelected>(OnNotifyGlobalMapCrusadeArmyInfoNextMergeArmySelected)
               .On<NotifyGlobalMapCrusadeArmyInfoPrevMergeArmySelected>(OnNotifyGlobalMapCrusadeArmyInfoPrevMergeArmySelected)
               .On<NotifyGlobalMapCrusadeArmyLeaderActionExecuted>(OnNotifyGlobalMapCrusadeArmyLeaderActionExecuted)
               .On<NotifyGlobalMapCrusadeArmiesMerging>(OnNotifyGlobalMapCrusadeArmiesMerging)
               .On<NotifyGlobalMapCrusadeArmyCreated>(OnNotifyGlobalMapCrusadeArmyCreated)
               .On<NotifyGlobalMapCrusadeArmyMainCartClosed>(OnNotifyGlobalMapCrusadeArmyMainCartClosed)
               .On<NotifyGlobalMapCrusadeArmyMergeCartClosed>(OnNotifyGlobalMapCrusadeArmyMergeCartClosed)
               .On<NotifyGlobalMapCrusadeArmyInfoCartNameChanged>(OnNotifyGlobalMapCrusadeArmyInfoCartNameChanged)
               .On<NotifyGlobalMapCrusadeArmySetLeaderClosed>(OnNotifyGlobalMapCrusadeArmySetLeaderClosed)
               .On<NotifyGlobalMapCrusadeArmySetLeaderClearClicked>(OnNotifyGlobalMapCrusadeArmySetLeaderClearClicked)
               .On<NotifyGlobalMapCrusadeArmySetLeaderRecruitClicked>(OnNotifyGlobalMapCrusadeArmySetLeaderRecruitClicked)
               .On<NotifyGlobalMapCrusadeArmyBuyLeaderClosed>(OnNotifyGlobalMapCrusadeArmyBuyLeaderClosed)
               .On<NotifyGlobalMapRecruitmentMercenariesRerolled>(OnNotifyGlobalMapRecruitmentMercenariesRerolled)
               .On<NotifyGlobalMapRecruitmentNextArmySelected>(OnNotifyGlobalMapRecruitmentNextArmySelected)
               .On<NotifyGlobalMapRecruitmentPrevArmySelected>(OnNotifyGlobalMapRecruitmentPrevArmySelected)
               .On<NotifyGlobalMapUnitsRecruited>(OnNotifyGlobalMapUnitsRecruited)
               .On<NotifyGlobalMapResourcesBought>(OnNotifyGlobalMapResourcesBought)
               .On<NotifyGlobalMapRecruitmentShown>(OnNotifyGlobalMapRecruitmentShown)
               .On<NotifyGlobalMapRecruitmentClosed>(OnNotifyGlobalMapRecruitmentClosed)
               .On<NotifyGlobalMapCrusadeArmyDismissed>(OnNotifyGlobalMapCrusadeArmyDismissed)
               .On<NotifyGlobalMapCrusadeArmyRecruitCartClosed>(OnNotifyGlobalMapCrusadeArmyRecruitCartClosed)
               .On<NotifyGlobalMapMagicSpellUsed>(OnNotifyGlobalMapMagicSpellUsed)
               .On<NotifyGlobalMapCrusadeArmyLeaderLevelingClosed>(OnNotifyGlobalMapCrusadeArmyLeaderLevelingClosed)
               .On<NotifyGlobalMapCrusadeArmyLeaderLevelingConfirmed>(OnNotifyGlobalMapCrusadeArmyLeaderLevelingConfirmed)
               .On<NotifyGlobalMapCrusadeArmyLeaderLevelingSkillSelected>(OnNotifyGlobalMapCrusadeArmyLeaderLevelingSkillSelected)
               .On<NotifyGlobalMapCommonPopupShown>(OnNotifyGlobalMapCommonPopupShown)

               // dialogs
               .On<NotifyDialogStarted>(OnNotifyDialogStarted)
               .On<NotifyDialogCueAnswerSuggested>(OnNotifyDialogCueAnswerSuggested)
               .On<NotifyDialogCueAnswerSelected>(OnNotifyDialogCueAnswerSelected)
               .On<NotifyDialogPopupClosed>(OnNotifyDialogPopupClosed)

               // vendor interaction
               .On<NotifyVendorDealMade>(OnNotifyVendorDealMade)
               .On<NotifyVendorWindowClosed>(OnNotifyVendorWindowClosed)

               // inspection
               .On<NotifyPerceptionCheckRolled>(OnNotifyPerceptionCheckRolled)
               .On<NotifyInspectionKnowledgeCheckRolled>(OnNotifyInspectionKnowledgeCheckRolled)
               .On<NotifyStealthPerceptionCheckRolled>(OnNotifyStealthPerceptionCheckRolled)

               // group management
               .On<NotifyGroupChangerClosed>(OnNotifyGroupChangerClosed)
               .On<NotifyGroupChangerUnitClicked>(OnNotifyGroupChangerUnitClicked)
               .On<NotifyGroupChangerPartyAccepted>(OnNotifyGroupChangerPartyAccepted)

               // skip time
               .On<NotifySkipTimeClosed>(OnNotifySkipTimeClosed)
               .On<NotifySkipTimeHoursChanged>(OnNotifySkipTimeHoursChanged)
               .On<NotifySkipTimeStarted>(OnNotifySkipTimeStarted)

               // zone loot
               .On<NotifyZoneLootCompleted>(OnNotifyZoneLootCompleted)
               .On<NotifyZoneLootLeft>(OnNotifyZoneLootLeft)
               .On<NotifyZoneLootRemoveToggleChanged>(OnNotifyZoneLootRemoveToggleChanged)

               // inventory
               .On<NotifyPolymorphicItemCreated>(OnNotifyPolymorphicItemCreated)
               ;
        }

        private async void OnNotifyGlobalMapCommonPopupShown(long receivedFrom, NotifyGlobalMapCommonPopupShown globalMapCommonPopupShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, Type={Type}, LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapCommonPopupShown), receivedFrom, globalMapCommonPopupShown.PlayerId, globalMapCommonPopupShown.Popup.Type, globalMapCommonPopupShown.Popup.Location?.Id.Length, globalMapCommonPopupShown.Popup.Location?.Name);
            AddPlayerToTracker(Game.PlayersInGlobalMapCommonPopup, globalMapCommonPopupShown.PlayerId);

            var popup = Mapper.Map<NetworkGlobalMapCommonPopup>(globalMapCommonPopupShown.Popup);

            var isShown = await GlobalMapInteraction.ShowCommonPopupAsync(popup);
            if (isShown)
            {
                OnGlobalMapCommonPopupShown(popup);
                return;
            }

            UpdateGlobalMapCommonPopupUIState(popup);
        }

        private async void OnNotifyCombatPreparationRequired(long receivedFrom, NotifyCombatPreparationRequired message)
        {
            Logger.LogInformation("Received {MessageType}. DiscrepantUnits={DiscrepantUnits}", nameof(NotifyCombatPreparationRequired), message.Discrepancy.Units);

            try
            {
                SetCombatStage(NetworkCombatStage.Preparing);

                var discrepancy = Mapper.Map<NetworkCombatUnitDiscrepancy>(message.Discrepancy);
                var isFixed = await FixCombatUnitDiscrepancyAsync(discrepancy);
                if (!isFixed)
                {
                    Logger.LogError("Discrepancy in combat start has not been fixed. DiscrepantUnits={DiscrepantUnits}", message.Discrepancy.Units);
                    return;
                }

                var units = CombatInteraction.GetUnitsInCombat();
                var confirmation = new ClientCombatPreparationCompleted
                {
                    PlayerId = Game.LocalPlayerId,
                    Units = Mapper.Map<List<Networking.Messages.Contracts.NetworkUnit>>(units)
                };
                Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, UnitsCount={UnitsCount}, Units={Units}", nameof(ClientCombatPreparationCompleted), confirmation.PlayerId, confirmation.Units.Count, confirmation.Units.Select(x => x.Id));
                Send(confirmation);

                SetCombatStage(NetworkCombatStage.Initialization);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while preparing for the combat");
                throw;
            }
        }

        private void OnNotifyGlobalMapCrusadeArmyLeaderLevelingSkillSelected(long receivedFrom, NotifyGlobalMapCrusadeArmyLeaderLevelingSkillSelected message)
        {
            Logger.LogInformation("Received {MessageType}. SkillId={SkillId}", nameof(NotifyGlobalMapCrusadeArmyLeaderLevelingSkillSelected), message.Id);

            GlobalMapInteraction.SelectLeaderLevelingSkill(message.Id);
        }

        private void OnNotifyGlobalMapCrusadeArmyLeaderLevelingConfirmed(long receivedFrom, NotifyGlobalMapCrusadeArmyLeaderLevelingConfirmed message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmyLeaderLevelingConfirmed));
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling);

            GlobalMapInteraction.ConfirmLeaderLeveling();
        }

        private void OnNotifyGlobalMapCrusadeArmyLeaderLevelingClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyLeaderLevelingClosed message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmyLeaderLevelingClosed));
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling);

            GlobalMapInteraction.CloseLeaderLeveling();
        }

        private void OnNotifyGlobalMapMagicSpellUsed(long receivedFrom, NotifyGlobalMapMagicSpellUsed message)
        {
            Logger.LogInformation("Received {MessageType}. SpellId={SpellId}, SpellName={SpellName}, TargetArmies={TargetArmies}", nameof(NotifyGlobalMapMagicSpellUsed), message.Spell.Id, message.Spell.Name, message.Spell.TargetArmies);
            var spell = Mapper.Map<NetworkGlobalMapMagicSpell>(message.Spell);

            GlobalMapInteraction.UseSpell(spell);
        }

        private void OnNotifyGlobalMapCrusadeArmyRecruitCartClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyRecruitCartClosed message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmyRecruitCartClosed));
            ResetPlayersTracker(Game.PlayersInGlobalMapRecruitment);
            GlobalMapInteraction.CloseRecruitments();
        }

        private void OnNotifyGlobalMapCrusadeArmyDismissed(long receivedFrom, NotifyGlobalMapCrusadeArmyDismissed message)
        {
            Logger.LogInformation("Received {MessageType}. ArmyId={SourceArmyId}", nameof(NotifyGlobalMapCrusadeArmyDismissed), message.Army.Id);
            var army = Mapper.Map<NetworkGlobalMapArmy>(message.Army);

            GlobalMapInteraction.DismissCrusadeArmy(army);
        }

        private void OnNotifyGlobalMapRecruitmentClosed(long receivedFrom, NotifyGlobalMapRecruitmentClosed message)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapRecruitmentClosed), message.PlayerId);
            // no need for specific removal as recruitment is already closed
            ResetPlayersTracker(Game.PlayersInGlobalMapRecruitment);

            GlobalMapInteraction.CloseRecruitments();
        }

        private void OnNotifyGlobalMapRecruitmentShown(long receivedFrom, NotifyGlobalMapRecruitmentShown message)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapRecruitmentShown), message.PlayerId);

            AddPlayerToTracker(Game.PlayersInGlobalMapRecruitment, message.PlayerId);
            UpdateGlobalMapRecruitmentUIState();

            GlobalMapInteraction.OpenRecruitments();
        }

        private void OnNotifyGlobalMapResourcesBought(long receivedFrom, NotifyGlobalMapResourcesBought message)
        {
            Logger.LogInformation("Received {MessageType}. FinalCost={FinalCost}, FinanceCount={FinanceCount}, MaterialsCount={MaterialsCount}", nameof(NotifyGlobalMapResourcesBought), message.Order.FinalCost, message.Order.FinanceCount, message.Order.MaterialCount);

            var globalMapResourceOrder = Mapper.Map<NetworkGlobalMapResourceOrder>(message.Order);

            GlobalMapInteraction.BuyResources(globalMapResourceOrder);
        }

        private void OnNotifyGlobalMapUnitsRecruited(long receivedFrom, NotifyGlobalMapUnitsRecruited message)
        {
            Logger.LogInformation("Received {MessageType}. UnitBlueprintId={UnitBlueprintId}, Count={Count}, ArmyId={ArmyId}, Type={Type}", nameof(NetworkGlobalMapUnitRecruitmentOrder), message.Order.BlueprintId, message.Order.Count, message.Order.ArmyId, message.Order.Type);

            var globalMapUnitRecruitmentOrder = Mapper.Map<NetworkGlobalMapUnitRecruitmentOrder>(message.Order);

            GlobalMapInteraction.BuyUnits(globalMapUnitRecruitmentOrder);
        }

        private void OnNotifyGlobalMapRecruitmentPrevArmySelected(long receivedFrom, NotifyGlobalMapRecruitmentPrevArmySelected message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapRecruitmentPrevArmySelected));

            GlobalMapInteraction.SelectPrevRecruitmentArmy();
        }

        private void OnNotifyGlobalMapRecruitmentNextArmySelected(long receivedFrom, NotifyGlobalMapRecruitmentNextArmySelected message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapRecruitmentNextArmySelected));

            GlobalMapInteraction.SelectNextRecruitmentArmy();
        }

        private void OnNotifyGlobalMapRecruitmentMercenariesRerolled(long receivedFrom, NotifyGlobalMapRecruitmentMercenariesRerolled message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapRecruitmentMercenariesRerolled));

            GlobalMapInteraction.RerollRecruitmentMercenaries();
        }

        private void OnNotifyGlobalMapCrusadeArmyBuyLeaderClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyBuyLeaderClosed globalMapCrusadeArmyBuyLeaderClosed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapTravelerModeChanged), globalMapCrusadeArmyBuyLeaderClosed.PlayerId);
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyBuyLeader, globalMapCrusadeArmyBuyLeaderClosed.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateAfterBuyLeader();

            GlobalMapInteraction.CloseBuyLeaderScreen();
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderRecruitClicked(long receivedFrom, NotifyGlobalMapCrusadeArmySetLeaderRecruitClicked globalMapCrusadeArmySetLeaderRecruitClicked)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmySetLeaderRecruitClicked));

            GlobalMapInteraction.ClickRecruitmentOnSetLeaderScreen();
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderClearClicked(long receivedFrom, NotifyGlobalMapCrusadeArmySetLeaderClearClicked globalMapCrusadeArmyInfoSetLeaderCleared)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmySetLeaderClearClicked));

            GlobalMapInteraction.ClearLeaderOnCrusdeArmyInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderClosed(long receivedFrom, NotifyGlobalMapCrusadeArmySetLeaderClosed globalMapCrusadeArmyInfoSetLeaderClosed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmySetLeaderClosed), globalMapCrusadeArmyInfoSetLeaderClosed.PlayerId);
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmySetLeader, globalMapCrusadeArmyInfoSetLeaderClosed.PlayerId);

            GlobalMapInteraction.CloseCrusadeArmySetLeaderInfo();

            UpdateGlobalMapCrusadeArmyInfoUIStateAfterSetLeader();
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoCartNameChanged(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoCartNameChanged message)
        {
            Logger.LogInformation("Received {MessageType}. ArmyId={ArmyId}, Name={Name}", nameof(NotifyGlobalMapCrusadeArmyInfoCartNameChanged), message.Army.Id, message.Army.Name);

            var army = Mapper.Map<NetworkGlobalMapArmy>(message.Army);

            GlobalMapInteraction.SetCrusadeArmyInfoCartName(army);
        }

        private void OnNotifyGlobalMapCrusadeArmyMainCartClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyMainCartClosed message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmyMainCartClosed));

            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyInfo);

            GlobalMapInteraction.CloseCrusadeArmyMainInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmyCreated(long receivedFrom, NotifyGlobalMapCrusadeArmyCreated message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmyCreated));

            GlobalMapInteraction.CreateCrusadeArmy();

            UpdateGlobalMapRecruitmentUIState();
            UpdateGlobalMapCrusadeArmyInfoUIState();
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoShown(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoShown message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmyInfoShown), receivedFrom, message.PlayerId);
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyInfo, message.PlayerId);

            GlobalMapInteraction.OpenCrusadeArmyInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmiesMerging(long receivedFrom, NotifyGlobalMapCrusadeArmiesMerging message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmiesMerging));

            GlobalMapInteraction.OpenCrusadeArmiesMergeScreen();
        }

        private void OnNotifyGlobalMapCrusadeArmyLeaderActionExecuted(long receivedFrom, NotifyGlobalMapCrusadeArmyLeaderActionExecuted message)
        {
            Logger.LogInformation("Received {MessageType}. LeaderId={LeaderId}, BlueprintId={BlueprintId}, Type={Type}", nameof(NotifyGlobalMapCrusadeArmyLeaderActionExecuted), message.Leader?.Id, message.Leader?.BlueprintId, message.Type);

            var leader = Mapper.Map<NetworkGlobalMapArmyLeader>(message.Leader);
            var actionType = Mapper.Map<NetworkGlobalMapArmyLeaderActionType>(message.Type);

            GlobalMapInteraction.RunLeaderAction(leader, actionType);
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoPrevMergeArmySelected(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoPrevMergeArmySelected globalMapCrusadeArmyInfoPrevMergeArmySelected)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmyInfoPrevMergeArmySelected));

            GlobalMapInteraction.SelectPrevCrusadeArmyInfoMergeArmy();
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoNextMergeArmySelected(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoNextMergeArmySelected globalMapCrusadeArmyInfoNextMergeArmySelected)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmyInfoNextMergeArmySelected));

            GlobalMapInteraction.SelectNextCrusadeArmyInfoMergeArmy();
        }

        private void OnNotifyGlobalMapCrusadeArmyMergeCartClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyMergeCartClosed globalMapCrusadeArmyInfoMergeClosed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmyMergeCartClosed), receivedFrom, globalMapCrusadeArmyInfoMergeClosed.PlayerId);
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyInfoMerge, globalMapCrusadeArmyInfoMergeClosed.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateAfterMerge();

            GlobalMapInteraction.CloseCrusadeArmyMergeInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmySquadsMovedToSecondArmy(long receivedFrom, NotifyGlobalMapCrusadeArmySquadsMovedToSecondArmy crusadeArmySquadsMovedToSecondArmy)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmySquadsMovedToSecondArmy));

            GlobalMapInteraction.MoveCrusadeArmySquadsToSecondArmy();
        }

        private void OnNotifyGlobalMapCrusadeArmySquadsMovedToMainArmy(long receivedFrom, NotifyGlobalMapCrusadeArmySquadsMovedToMainArmy crusadeArmySquadsMovedToMainArmy)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmySquadsMovedToMainArmy));

            GlobalMapInteraction.MoveCrusadeArmySquadsToMainArmy();
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoClosed globalMapCrusadeArmyInfoClosed)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmyInfoClosed));

            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyInfo);
            GlobalMapInteraction.CloseCrusadeArmyInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmySquadDismissed(long receivedFrom, NotifyGlobalMapCrusadeArmySquadDismissed crusadeArmySquadDismissed)
        {
            Logger.LogInformation("Received {MessageType}. ArmyId={SourceArmyId}, SquadId={SourceSquadId}, Position={SourcePosition}", nameof(NotifyGlobalMapCrusadeArmySquadDismissed), crusadeArmySquadDismissed.SquadSlot.ArmyId, crusadeArmySquadDismissed.SquadSlot.SquadId, crusadeArmySquadDismissed.SquadSlot.Position);

            var squadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadDismissed.SquadSlot);

            GlobalMapInteraction.DismissCrusadeArmySquad(squadSlot);
        }

        private void OnNotifyGlobalMapCrusadeArmyMergedInOne(long receivedFrom, NotifyGlobalMapCrusadeArmyMergedInOne crusadeArmyMergedInOne)
        {
            Logger.LogInformation("Received {MessageType}. SourceArmyId={SourceArmyId}, SourceSquadId={SourceSquadId}, SourcePosition={SourcePosition}", nameof(NotifyGlobalMapCrusadeArmyMergedInOne),
                crusadeArmyMergedInOne.SquadSlot.ArmyId, crusadeArmyMergedInOne.SquadSlot.SquadId, crusadeArmyMergedInOne.SquadSlot.Position);

            var squadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmyMergedInOne.SquadSlot);

            GlobalMapInteraction.MergeInOneCrusadeArmySquad(squadSlot);
        }

        private void OnNotifyGlobalMapCrusadeArmySquadSplitRequested(long receivedFrom, NotifyGlobalMapCrusadeArmySquadSplitRequested crusadeArmySquadSplitRequested)
        {
            Logger.LogInformation("Received {MessageType}. SourceArmyId={SourceArmyId}, SourceSquadId={SourceSquadId}, SourcePosition={SourcePosition}, TargetArmyId={TargetArmyId}, TargetSquadId={TargetSquadId}, TargetPosition={TargetPosition}, Count={Count}", nameof(NotifyGlobalMapCrusadeArmySquadSplitRequested),
                crusadeArmySquadSplitRequested.SourceSquadSlot.ArmyId, crusadeArmySquadSplitRequested.SourceSquadSlot.SquadId, crusadeArmySquadSplitRequested.SourceSquadSlot.Position, crusadeArmySquadSplitRequested.TargetSquadSlot.ArmyId, crusadeArmySquadSplitRequested.TargetSquadSlot.SquadId, crusadeArmySquadSplitRequested.TargetSquadSlot.Position, crusadeArmySquadSplitRequested.Count);

            var sourceSquadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadSplitRequested.SourceSquadSlot);
            var targetSquadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadSplitRequested.TargetSquadSlot);

            GlobalMapInteraction.RunSplitRequestForCrusadeArmySquad(sourceSquadSlot, targetSquadSlot, crusadeArmySquadSplitRequested.Count);
        }

        private void OnNotifyGlobalMapCrusadeArmySquadsSwitched(long receivedFrom, NotifyGlobalMapCrusadeArmySquadsSwitched crusadeArmySquadsSwitched)
        {
            Logger.LogInformation("Received {MessageType}. SourceArmyId={SourceArmyId}, SourceSquadId={SourceSquadId}, SourcePosition={SourcePosition}, TargetArmyId={TargetArmyId}, TargetSquadId={TargetSquadId}, TargetPosition={TargetPosition}", nameof(NotifyGlobalMapCrusadeArmySquadsSwitched),
                crusadeArmySquadsSwitched.SourceSquadSlot.ArmyId, crusadeArmySquadsSwitched.SourceSquadSlot.SquadId, crusadeArmySquadsSwitched.SourceSquadSlot.Position, crusadeArmySquadsSwitched.TargetSquadSlot.ArmyId, crusadeArmySquadsSwitched.TargetSquadSlot.SquadId, crusadeArmySquadsSwitched.TargetSquadSlot.Position);

            var sourceSquadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadsSwitched.SourceSquadSlot);
            var targetSquadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadsSwitched.TargetSquadSlot);

            GlobalMapInteraction.SwitchCrusadeArmySquads(sourceSquadSlot, targetSquadSlot);
        }

        private void OnNotifyGlobalMapCrusadeArmySquadsMerged(long receivedFrom, NotifyGlobalMapCrusadeArmySquadsMerged crusadeArmySquadsMerged)
        {
            Logger.LogInformation("Received {MessageType}. SourceArmyId={SourceArmyId}, SourceSquadId={SourceSquadId}, SourcePosition={SourcePosition}, TargetArmyId={TargetArmyId}, TargetSquadId={TargetSquadId}, TargetPosition={TargetPosition}, Count={Count}", nameof(NotifyGlobalMapCrusadeArmySquadsMerged),
                crusadeArmySquadsMerged.SourceSquadSlot.ArmyId, crusadeArmySquadsMerged.SourceSquadSlot.SquadId, crusadeArmySquadsMerged.SourceSquadSlot.Position, crusadeArmySquadsMerged.TargetSquadSlot.ArmyId, crusadeArmySquadsMerged.TargetSquadSlot.SquadId, crusadeArmySquadsMerged.TargetSquadSlot.Position, crusadeArmySquadsMerged.Count);

            var sourceSquadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadsMerged.SourceSquadSlot);
            var targetSquadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadsMerged.TargetSquadSlot);

            GlobalMapInteraction.MergeCrusadeArmySquads(sourceSquadSlot, targetSquadSlot, crusadeArmySquadsMerged.Count);
        }

        private void OnNotifyGlobalMapCrusadeArmySquadSplitted(long receivedFrom, NotifyGlobalMapCrusadeArmySquadSplitted crusadeArmySquadSplitted)
        {
            Logger.LogInformation("Received {MessageType}. SourceArmyId={SourceArmyId}, SourceSquadId={SourceSquadId}, SourcePosition={SourcePosition}, Count={Count}", nameof(NotifyGlobalMapCrusadeArmySquadSplitted),
                crusadeArmySquadSplitted.SquadSlot.ArmyId, crusadeArmySquadSplitted.SquadSlot.SquadId, crusadeArmySquadSplitted.SquadSlot.Position, crusadeArmySquadSplitted.Count);

            var squadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadSplitted.SquadSlot);

            GlobalMapInteraction.SplitCrusadeArmySquad(squadSlot, crusadeArmySquadSplitted.Count);
        }

        private void OnNotifyTacticalCombatRetreated(long receivedFrom, NotifyTacticalCombatRetreated tacticalCombatRetreated)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyTacticalCombatRetreated));

            CombatInteraction.RetreatFromTacticalCombat();
        }

        private void OnNotifyTacticalCombatTotalDefenseUsed(long receivedFrom, NotifyTacticalCombatTotalDefenseUsed tacticalCombatTotalDefenseUsed)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyTacticalCombatTotalDefenseUsed));

            CombatInteraction.UseTacticalCombatTotalDefense();
        }

        private void OnNotifyTacticalCombatTurnPostponed(long receivedFrom, NotifyTacticalCombatTurnPostponed tacticalCombatTurnPostponed)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyTacticalCombatTurnPostponed));

            CombatInteraction.PostponeTacticalCombatTurn();
        }

        private void OnNotifyTacticalUnitMoveToCommandExecuted(long receivedFrom, NotifyTacticalUnitMoveToCommandExecuted tacticalUnitMoveToCommandExecuted)
        {
            Logger.LogInformation("Received {MessageType}. UnitId={UnitId}, Path={Path}", nameof(NotifyTacticalUnitMoveToCommandExecuted), tacticalUnitMoveToCommandExecuted.Command.UnitId, tacticalUnitMoveToCommandExecuted.Command.Path);
            var command = Mapper.Map<NetworkTacticalUnitMoveToCommand>(tacticalUnitMoveToCommandExecuted.Command);

            CombatInteraction.RunTacticalUnitMoveToCommand(command);
        }

        private void OnNotifyTacticalUnitUseAbilityCommandExecuted(long receivedFrom, NotifyTacticalUnitUseAbilityCommandExecuted tacticalUnitUseAbilityCommandExecuted)
        {
            Logger.LogInformation("Received {MessageType}. UnitId={UnitId}, AbilityId={AbilityId}, Path={Path}", nameof(NotifyTacticalUnitUseAbilityCommandExecuted), tacticalUnitUseAbilityCommandExecuted.Command.InitiatorUnitId, tacticalUnitUseAbilityCommandExecuted.Command.Ability.Id, tacticalUnitUseAbilityCommandExecuted.Command.VectorPath);
            var command = Mapper.Map<NetworkTacticalUnitUseAbilityCommand>(tacticalUnitUseAbilityCommandExecuted.Command);

            CombatInteraction.RunTacticalUnitUseAbilityCommand(command);
        }

        private void OnNotifyTacticalUnitAttackCommandExecuted(long receivedFrom, NotifyTacticalUnitAttackCommandExecuted tacticalUnitAttackCommandExecuted)
        {
            Logger.LogInformation("Received {MessageType}. UnitId={UnitId}, TargetId={TargetId}, Path={Path}", nameof(NotifyTacticalUnitAttackCommandExecuted), tacticalUnitAttackCommandExecuted.Command.UnitId, tacticalUnitAttackCommandExecuted.Command.TargetUnitId, tacticalUnitAttackCommandExecuted.Command.Path);
            var command = Mapper.Map<NetworkTacticalUnitAttackCommand>(tacticalUnitAttackCommandExecuted.Command);

            CombatInteraction.RunTacticalUnitAttackCommand(command);
        }

        private void OnNotifyGlobalMapCombatResultsClosed(long receivedFrom, NotifyGlobalMapCombatResultsClosed globalMapCombatResultsClosed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}", nameof(NotifyGlobalMapCombatResultsClosed), receivedFrom);
            ResetPlayersTracker(Game.PlayersInGlobalMapCombatResults);
            GlobalMapInteraction.CloseCombatResults();
        }

        private void OnNotifyCrusadeArmyBattleResultsClosed(long receivedFrom, NotifyCrusadeArmyBattleResultsClosed crusadeArmyBattleResultsClosed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}", nameof(NotifyCrusadeArmyBattleResultsClosed), receivedFrom);
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyBattleResults);
            GlobalMapInteraction.CloseCrusadeArmyBattleResults();
        }

        private void OnNotifyCrusadeArmyBattleResultsManualCombatStarted(long receivedFrom, NotifyCrusadeArmyBattleResultsManualCombatStarted crusadeArmyBattleResultsManualCombatStarted)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}", nameof(NotifyCrusadeArmyBattleResultsManualCombatStarted), receivedFrom);
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyBattleResults);

            GlobalMapInteraction.StartCrusadeArmyBattleResultsManualCombat();
        }

        private async void OnNotifyTacticalCombatInitialized(long receivedFrom, NotifyTacticalCombatInitialized tacticalCombatInitialized)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, AreaSeed={AreaSeed}, Seed={Seed}", nameof(NotifyTacticalCombatInitialized), receivedFrom, tacticalCombatInitialized.AreaSeed, tacticalCombatInitialized.Seed);

            await WaitWhileTrue(() => Game.ArmyCombat == null || !CombatInteraction.IsInCrusadeTacticalCombat(), "Crusade army combat has not been started yet");

            Game.ArmyCombat.AreaSeed = tacticalCombatInitialized.AreaSeed;
            Game.ArmyCombat.Seed = tacticalCombatInitialized.Seed;
            CombatInteraction.InitializeCrusadeArmyCombat();
        }

        private void OnNotifyGlobalMapAutoCrusadeCombatChanged(long receivedFrom, NotifyGlobalMapAutoCrusadeCombatChanged globalMapAutoCrusadeCombatChanged)
        {
            Logger.LogInformation("Received {MessageType}. IsEnabled={IsEnabled}", nameof(NotifyGlobalMapAutoCrusadeCombatChanged), globalMapAutoCrusadeCombatChanged.IsEnabled);

            GlobalMapInteraction.SetAutoCrusadeCombat(globalMapAutoCrusadeCombatChanged.IsEnabled);
        }

        private void OnNotifyGlobalMapSelectedArmyChanged(long receivedFrom, NotifyGlobalMapSelectedArmyChanged globalMapSelectedArmyChanged)
        {
            Logger.LogInformation("Received {MessageType}. ArmyId={ArmyId}", nameof(NotifyGlobalMapSelectedArmyChanged), globalMapSelectedArmyChanged.Army?.Id);

            var army = Mapper.Map<NetworkGlobalMapArmy>(globalMapSelectedArmyChanged.Army);

            GlobalMapInteraction.SetSelectedArmy(army);
        }

        private void OnNotifyGlobalMapTravelerModeChanged(long receivedFrom, NotifyGlobalMapTravelerModeChanged globalMapTravelerModeChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, TravelerMode={TravelerMode}, MustBeEnforced={MustBeEnforced}", nameof(NotifyGlobalMapTravelerModeChanged), globalMapTravelerModeChanged.PlayerId, globalMapTravelerModeChanged.TravelerMode, globalMapTravelerModeChanged.MustBeEnforced);

            var travelerMode = Mapper.Map<NetworkGlobalMapTravelerMode>(globalMapTravelerModeChanged.TravelerMode);
            RegisterGlobalMapMode(globalMapTravelerModeChanged.PlayerId, travelerMode);
            UpdateGlobalMapUIState();

            if (globalMapTravelerModeChanged.MustBeEnforced)
            {
                GlobalMapInteraction.ChangeArmyMode(travelerMode);
            }
        }

        private void OnNotifyGlobalMapDaySkipped(long receivedFrom, NotifyGlobalMapDaySkipped globalMapDaySkipped)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapDaySkipped));

            GlobalMapInteraction.SkipDay();
        }

        private void OnNotifyGlobalMapCommonPopupDeclined(long receivedFrom, NotifyGlobalMapCommonPopupDeclined messsage)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Type={Type}, LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapCommonPopupDeclined), messsage.PlayerId, messsage.Popup.Type, messsage.Popup.Location?.Id, messsage.Popup.Location?.Name);

            RemovePlayerFromTracker(Game.PlayersInGlobalMapCommonPopup, messsage.PlayerId);

            GlobalMapInteraction.DeclineCommonPopup();
        }


        private void OnNotifyGlobalMapLocationMessageAccepted(long playerId, NotifyGlobalMapLocationMessageAccepted message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapLocationMessageClosed));

            ResetPlayersTracker(Game.PlayersInGlobalMapLocationMessage);

            GlobalMapInteraction.AcceptLocationMessageBox();
        }

        private void OnNotifyGlobalMapLocationMessageClosed(long playerId, NotifyGlobalMapLocationMessageClosed globalMapLocationMessageClosed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapLocationMessageClosed), globalMapLocationMessageClosed.PlayerId);

            RemovePlayerFromTracker(Game.PlayersInGlobalMapLocationMessage, globalMapLocationMessageClosed.PlayerId);

            GlobalMapInteraction.CloseLocationMessageBox();
        }

        private void OnNotifyPolymorphicItemCreated(long playerId, NotifyPolymorphicItemCreated polymorphicItemCreated)
        {
            Logger.LogInformation("Received {MessageType}. UnitId={UnitId}, ItemName={ItemName}, SlotType={SlotType}", nameof(NotifyPolymorphicItemCreated), polymorphicItemCreated.PolymorphicItem.UnitId, polymorphicItemCreated.PolymorphicItem.Item.Name, polymorphicItemCreated.PolymorphicItem.Position.Type);

            var polymorphicItem = Mapper.Map<NetworkPolymorphicItem>(polymorphicItemCreated.PolymorphicItem);
            GameInteraction.CreateAndEquipPolymorphicItem(polymorphicItem, createContext: true);
        }

        private void OnNotifyNewGameSequenceTerminated(long playerId, NotifyNewGameSequenceTerminated newGameSequenceTerminated)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyNewGameSequenceTerminated));

            GameInteraction.TerminateNewGameSequence();
        }

        private void OnNotifyNewGameSequenceLevelingStarted(long playerId, NotifyNewGameSequenceLevelingStarted newGameSequenceLevelingStarted)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyNewGameSequenceLevelingStarted), playerId);

            GameInteraction.StartNewGameSequenceLeveling();
        }

        private void OnNotifyNewGameSequencePhaseChanged(long playerId, NotifyNewGameSequencePhaseChanged newGameSequencePhaseChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, PhaseType={PhaseType}", nameof(NotifyNewGameSequencePhaseChanged), newGameSequencePhaseChanged.Phase.Type);

            var phase = Mapper.Map<NetworkNewGameSequencePhase>(newGameSequencePhaseChanged.Phase);

            GameInteraction.SelectNewGameSequencePhase(phase);
        }

        private void OnNotifyNewGameDifficultyChanged(long playerId, NotifyNewGameDifficultyChanged newGameDifficultyChanged)
        {
            Logger.LogInformation("Received {MessageType}. Difficulty={Difficulty}", nameof(NotifyNewGameDifficultyChanged), newGameDifficultyChanged);

            GameInteraction.SelectNewGameDifficulty(newGameDifficultyChanged.Difficulty);
        }

        private void OnNotifyCharacterSelectionWindowClosed(long playerId, NotifyCharacterSelectionWindowClosed characterSelectionWindowClosed)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyCharacterSelectionWindowClosed));

            GameInteraction.CloseCharacterSelectionWindow();
        }

        private void OnNotifyCharacterSelectionWindowAccepted(long playerId, NotifyCharacterSelectionWindowAccepted characterSelectionWindowAccepted)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyCharacterSelectionWindowAccepted));

            GameInteraction.AcceptCharacterSelectionWindow();
        }

        private void OnNotifyCharacterSelectionToggleChanged(long playerId, NotifyCharacterSelectionToggleChanged characterSelectionToggleChanged)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyCharacterSelectionToggleChanged));

            GameInteraction.ToggleCharacterSelectionWindow(characterSelectionToggleChanged.UnitId);
        }

        private void OnNotifyZoneLootRemoveToggleChanged(long receivedFrom, NotifyZoneLootRemoveToggleChanged zoneLootRemoveToggleChanged)
        {
            Logger.LogInformation("Received {MessageType}. RemoveLoot={RemoveLoot}", nameof(NotifyZoneLootRemoveToggleChanged), zoneLootRemoveToggleChanged.RemoveLoot);

            GameInteraction.UpdateZoneLootRemoveToggle(zoneLootRemoveToggleChanged.RemoveLoot);
        }

        private void OnNotifyZoneLootLeft(long receivedFrom, NotifyZoneLootLeft zoneLootLeft)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyZoneLootLeft));

            GameInteraction.LeaveZoneLoot();
        }

        private void OnNotifyZoneLootCompleted(long receivedFrom, NotifyZoneLootCompleted zoneLootCompleted)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyZoneLootCompleted));

            GameInteraction.CompleteZoneLoot();
        }

        private void OnNotifyGlobalMapEncounterRolled(long receivedFrom, NotifyGlobalMapEncounterRolled message)
        {
            Logger.LogInformation("Received {MessageType}. Seed={Seed}, EncounterId={EncounterId}, Position={Position}, Avoidance={Avoidance}", nameof(NotifyGlobalMapEncounterRolled), message.Encounter.Seed, message.Encounter.BlueprintId, message.Encounter.Position, message.Encounter.AvoidanceResult);
            var encounter = Mapper.Map<NetworkGlobalMapEncounter>(message.Encounter);

            GlobalMapInteraction.RollEncounter(encounter);
        }

        private void OnNotifyGlobalMapEncounterAvoided(long receivedFrom, NotifyGlobalMapEncounterAvoided globalMapEncounterAvoided)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapEncounterAvoided));
            GlobalMapInteraction.AvoidEncounter();

            ResetPlayersTracker(Game.PlayersInGlobalMapEncounterMessage);
        }

        private void OnNotifyGlobalMapEncounterAccepted(long receivedFrom, NotifyGlobalMapEncounterAccepted notifyGlobalMapEncounterAccepted)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapEncounterAccepted));
            GlobalMapInteraction.AcceptEncounter();

            ResetPlayersTracker(Game.PlayersInGlobalMapEncounterMessage);
        }

        private void OnNotifyGlobalMapIngredientCollectionAccepted(long receivedFrom, NotifyGlobalMapCommonPopupAccepted globalMapCommonPopupAccepted)
        {
            Logger.LogInformation("Received {MessageType}. Type={Type}, LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapCommonPopupAccepted), globalMapCommonPopupAccepted.Popup.Type, globalMapCommonPopupAccepted.Popup.Location?.Id, globalMapCommonPopupAccepted.Popup.Location?.Name);

            var popup = Mapper.Map<NetworkGlobalMapCommonPopup>(globalMapCommonPopupAccepted.Popup);
            GlobalMapInteraction.AcceptCommonPopup(popup);

            ResetPlayersTracker(Game.PlayersInGlobalMapCommonPopup);
        }

        private void OnNotifyGlobalMapTravelContinued(long receivedFrom, NotifyGlobalMapTravelContinued globalMapTravelContinued)
        {
            Logger.LogInformation("Received {MessageType}. EdgePosition={EdgePosition}", nameof(NotifyGlobalMapTravelContinued), globalMapTravelContinued.Traveler.Position?.EdgePosition);
            var traveler = Mapper.Map<NetworkGlobalMapTraveler>(globalMapTravelContinued.Traveler);
            GlobalMapInteraction.ContinueTravel(traveler);
        }

        private void OnNotifyGlobalMapTravelStopped(long receivedFrom, NotifyGlobalMapTravelStopped globalMapTravelStopped)
        {
            Logger.LogInformation("Received {MessageType}. EdgePosition={EdgePosition}", nameof(NotifyGlobalMapTravelStopped), globalMapTravelStopped.Traveler.Position?.EdgePosition);

            var traveler = Mapper.Map<NetworkGlobalMapTraveler>(globalMapTravelStopped.Traveler);
            GlobalMapInteraction.StopTravel(traveler);
        }

        private void OnNotifySkipTimeStarted(long receivedFrom, NotifySkipTimeStarted skipTimeStarted)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifySkipTimeStarted));
            ResetPlayersTracker(Game.PlayersInSkipTime);
            GameInteraction.StartSkipTime();
        }

        private void OnNotifySkipTimeHoursChanged(long receivedFrom, NotifySkipTimeHoursChanged skipTimeHoursChanged)
        {
            Logger.LogInformation("Received {MessageType}. Hours={Hours}", nameof(NotifySkipTimeHoursChanged), skipTimeHoursChanged.Hours);
            GameInteraction.UpdateSkipTimeHours(skipTimeHoursChanged.Hours);
        }

        private void OnNotifySkipTimeClosed(long receivedFrom, NotifySkipTimeClosed skipTimeClosed)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifySkipTimeClosed));
            GameInteraction.CloseSkipTimeUI();
            ResetPlayersTracker(Game.PlayersInSkipTime);
        }

        private void OnNotifyGlobalMapTravelStarted(long receivedFrom, NotifyGlobalMapTravelStarted globalMapTravelStarted)
        {
            Logger.LogInformation("Received {MessageType}. Type={Type}, MovementPoints={MovementPoints}, FromClick={FromClick}, DestinationId={DestinationId}, DestinationName={DestinationName}", nameof(NotifyGlobalMapTravelStarted), globalMapTravelStarted.Travel.Type, globalMapTravelStarted.Travel.Traveler.MovementPoints, globalMapTravelStarted.Travel.FromClick, globalMapTravelStarted.Travel.Destination.Id, globalMapTravelStarted.Travel.Destination.Name);

            var travel = Mapper.Map<NetworkGlobalMapTravel>(globalMapTravelStarted.Travel);

            GlobalMapInteraction.StartTravel(travel);
        }

        private void OnNotifyGlobalMapRestOpened(long receivedFrom, NotifyGlobalMapRestOpened message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapRestOpened));
            GameInteraction.InitiateRest();
        }

        private void OnNotifyGlobalMapGroupChangerOpened(long receivedFrom, NotifyGlobalMapGroupChangerOpened message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapGroupChangerOpened));
            GlobalMapInteraction.OpenGroupChanger();
        }

        private void OnNotifyRestWindowClosed(long receivedFrom, NotifyRestWindowClosed message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyRestWindowClosed));
            GameInteraction.CloseRestWindow();
        }

        private void OnNotifyGroupChangerPartyAccepted(long playerId, NotifyGroupChangerPartyAccepted groupChangerPartyAccepted)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGroupChangerPartyAccepted));
            GameInteraction.AcceptGroupChangerParty();
            ResetPlayersTracker(Game.PlayersInGroupChanger);
        }

        private void OnNotifyGroupChangerUnitClicked(long playerId, NotifyGroupChangerUnitClicked groupChangerUnitClicked)
        {
            Logger.LogInformation("Received {MessageType}. UnitId={UnitId}", nameof(NotifyGroupChangerUnitClicked), groupChangerUnitClicked.UnitId);
            GameInteraction.ClickGroupChangerUnit(groupChangerUnitClicked.UnitId);
        }

        private void OnNotifyGroupChangerClosed(long playerId, NotifyGroupChangerClosed groupChangerClosed)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGroupChangerClosed));

            GameInteraction.CloseGroupChangerUI();
            ResetPlayersTracker(Game.PlayersInGroupChanger);
        }

        private void OnNotifyLobbySyncStatusChanged(long receivedFrom, NotifyLobbySyncStatusChanged lobbySyncStatusChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Status={Status}", nameof(NotifyLobbySyncStatusChanged), lobbySyncStatusChanged.PlayerId, lobbySyncStatusChanged.Status);

            var status = Mapper.Map<NetworkLobbySyncStatus>(lobbySyncStatusChanged.Status);
            UpdateLobbySyncStatus(lobbySyncStatusChanged.PlayerId, status);
        }

        private void OnNotifyLevelingStarted(long playerId, NotifyLevelingStarted characterLevelingStarted)
        {
            Logger.LogInformation("Received {MessageType}. UnitId={unitId}, Type={Type}", nameof(NotifyLevelingStarted), characterLevelingStarted.UnitId, characterLevelingStarted.Type);

            if (!Enum.TryParse<NetworkLevelingType>(characterLevelingStarted.Type, true, out var levelingType))
            {
                Logger.LogError("Invalid leveling type value. Value={Value}", characterLevelingStarted.Type);
                return;
            }

            InitiateLeveling(characterLevelingStarted.UnitId, levelingType);
            LevelingInteraction.StartLeveling(Game.Leveling.UnitId, Game.Leveling.Type);
        }

        private void OnNotifyVendorWindowClosed(long playerId, NotifyVendorWindowClosed closed)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyVendorWindowClosed));
            GameInteraction.CloseVendorWindow();
        }

        private void OnNotifyVendorDealMade(long playerId, NotifyVendorDealMade made)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyVendorDealMade));
            GameInteraction.MakeVendorDeal();
        }

        private void OnNotifyInvalidCombatTurnStarted(long playerId, NotifyInvalidCombatTurnStarted message)
        {
            Logger.LogInformation("Received {MessageType}. UnitId={UnitId}", nameof(NotifyInvalidCombatTurnStarted), message.UnitId);
            var characterName = GameInteraction.GetUnitCharacterName(message.UnitId);
            PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.Turn.ClientOrderDesync.Key, CombatTextSeverity.Debug, new UnitEntityLog(message.UnitId));
            ResetCombatTurn();
            CombatInteraction.StartTurnBasedCombatTurn(message.UnitId);
        }

        private void OnNotifyRestStarted(long playerId, NotifyRestStarted started)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyRestStarted));
            GameInteraction.StartRest();
        }

        private void OnNotifyCampingUnitsRoleChanged(long playerId, NotifyCampingUnitsRoleChanged changed)
        {
            var rolesData = string.Join(" ,", changed.Roles.Select(r => $"[RoleType={r.RoleType} PrimaryUnit={r.PrimaryUnitId} SecondaryUnit={r.SecondaryUnitId}]"));
            Logger.LogInformation("Received {MessageType}. RolesCount={rolesCount}, RolesData={rolesData}", nameof(NotifyCampingUnitsRoleChanged), changed.Roles.Count, rolesData);

            var roles = Mapper.Map<List<NetworkCampingRole>>(changed.Roles);
            GameInteraction.SetCampingRoles(roles);
        }

        private void OnNotifyCampingStateChanged(long playerId, NotifyCampingStateChanged changed)
        {
            Logger.LogInformation("Received {MessageType}. CookingBlueprintRecipeId={CookingBlueprintRecipeId}, PotionBlueprintRecipeId={PotionBlueprintRecipeId}, ScrollBlueprintRecipeId={ScrollBlueprintRecipeId}, IterationsCount={IterationsCount}, AutotuneIterations={AutotuneIterations}", nameof(NotifyCampingStateChanged),
                changed.State.CookingBlueprintRecipeId, changed.State.PotionBlueprintRecipeId, changed.State.ScrollBlueprintRecipeId, changed.State.IterationsCount, changed.State.AutotuneIterationsStatus);

            var state = Mapper.Map<NetworkCampingState>(changed.State);
            GameInteraction.SetCampingState(state);
        }

        private void OnNotifyCampingUseHealingSpellsChanged(long playerId, NotifyCampingUseHealingSpellsChanged changed)
        {
            Logger.LogInformation("Received {MessageType}. IsOn={IsOn}", nameof(NotifyCampingUseHealingSpellsChanged), changed.IsOn);
            GameInteraction.SetCampingUseHealingSpells(changed.IsOn);
        }

        private void OnNotifySpawnCampPlace(long playerId, NotifySpawnCampPlace place)
        {
            Logger.LogInformation("Received {MessageType}. Position={Position}", nameof(NotifySpawnCampPlace), place.Position);
            var position = Mapper.Map<NetworkVector3>(place.Position);
            GameInteraction.SpawnCampPlace(position);
        }

        private void OnNotifyInspectionKnowledgeCheckRolled(long playerId, NotifyInspectionKnowledgeCheckRolled rolled)
        {
            Logger.LogInformation("Received {MessageType}. TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}, StatType={StatType}, DC={DC}, RollResult={RollResult}", nameof(NotifyInspectionKnowledgeCheckRolled), rolled.Check.TargetUnitId, rolled.Check.InitiatorUnitId, rolled.Check.StatType, rolled.Check.DC, rolled.Check.RollResult);
            var check = Mapper.Map<NetworkInspectionKnowledgeCheck>(rolled.Check);
            GameInteraction.ApplyInspectionKnowledgeCheck(check);
        }

        private void OnNotifyPerceptionCheckRolled(long playerId, NotifyPerceptionCheckRolled rolled)
        {
            Logger.LogInformation("Received {MessageType}. UnitId={UnitId}, MapObjectId={MapObjectId}", nameof(NotifyPerceptionCheckRolled), rolled.Check.UnitId, rolled.Check.MapObject.Id);

            var check = Mapper.Map<NetworkPerceptionCheck>(rolled.Check);
            GameInteraction.ApplyPerceptionCheck(check);
        }

        private void OnNotifyStealthPerceptionCheckRolled(long playerId, NotifyStealthPerceptionCheckRolled rolled)
        {
            Logger.LogInformation("Received {MessageType}. InitiatorId={InitiatorId}, Roll={Roll}, StealthedUnitId={StealthedUnitId}, IsSuccess={IsSuccess}", nameof(NotifyStealthPerceptionCheckRolled), rolled.Check.InitiatorId, rolled.Check.Roll, rolled.Check.StealthedUnitId, rolled.Check.IsSuccess);

            var check = Mapper.Map<NetworkStealthPerceptionCheck>(rolled.Check);
            GameInteraction.ApplyStealthPerceptionCheck(check);
        }

        private async void OnNotifyCombatTurnSynchronizationRequired(long playerId, NotifyCombatTurnSynchronizationRequired message)
        {
            Logger.LogInformation("Received {MessageType}. Units={Units}, TurnSeed={TurnSeed}, TriggeredAreaEffects={TriggeredAreaEffects}", nameof(NotifyCombatTurnSynchronizationRequired), message.CombatState.Units.Count, message.TurnSeed, message.TriggeredAreaEffects);

            try
            {
                await WaitWhileTrue(() => Game.Combat?.Turn == null, "Turn has not been initialized yet");

                var combatState = Mapper.Map<NetworkCombatState>(message.CombatState);
                var areaEffects = Mapper.Map<List<NetworkAreaEffect>>(message.TriggeredAreaEffects);
                Game.Combat.TriggeredAreaEffects.AddRange(areaEffects);

                await CombatInteraction.UpdateCombatStateAsync(combatState, areaEffects, false);

                Game.Combat.Turn.Seed = message.TurnSeed;

                Game.Combat.AIActions.Clear();
                DiceRollStorage.Reset();
                Logger.LogInformation("Dice roll storage has been reset at after syncing turn units");

                ValueGenerator.ResetSeededGenerators(Random.IdentifierLifetime.CombatTurn);

                var confirmationMessage = new ClientCombatTurnSynchronized { UnitId = Game.Combat.Turn.UnitId };
                Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}", nameof(ClientCombatTurnSynchronized), confirmationMessage.UnitId);
                Send(confirmationMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to sync combat turn");
                throw;
            }
        }

        private async void OnDiceRollValueRequest(long playerId, DiceRollValueRequest request)
        {
            Logger.LogInformation("Received {MessageType}. RollId={RollId}, PlayerId={PlayerId}", nameof(DiceRollValueRequest), request.RollId, request.PlayerId);
            await SendLocalRollAsync(request.PlayerId, request);
        }

        private void OnNotifyCombatTurnStarted(long playerId, NotifyCombatTurnStarted message)
        {
            Logger.LogInformation("Received {MessageType}. Round={Round}, UnitId={UnitId}", nameof(NotifyCombatTurnStarted), message.Round, message.UnitId);
            if (Game.Combat?.Turn == null)
            {
                Logger.LogError("Trying to start not initialized turn. Round={Round}, UnitId={UnitId}", message.Round, message.UnitId);
                return;
            }

            if (!string.Equals(message.UnitId, Game.Combat.Turn.UnitId, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning("Starting turn with different UnitId. LocalUnitId={LocalUnitId}, HostUnitId={HostUnitId}", Game.Combat.Turn.UnitId, message.UnitId);
            }

            if (Game.Combat.Round != message.Round)
            {
                Logger.LogWarning("Starting turn with different Round number. LocalRound={LocalRound}, HostRound={HostRound}", Game.Combat.Round, message.Round);
            }

            Logger.LogInformation("Starting combat turn. UnitId={UnitId}, TurnSeed={TurnSeed}", Game.Combat.Turn.Seed, Game.Combat.Turn.Seed);
            Game.Combat.Turn.IsInProgress = true;
            CombatInteraction.StartTurnBasedCombatTurn(Game.Combat.Turn.UnitId);
        }

        private async void OnNotifyCombatInitializationRequired(long playerId, NotifyCombatInitializationRequired message)
        {
            Logger.LogInformation("Received {MessageType}. CombatSeed={CombatSeed}, UnitsCount={UnitsCount}, Units={Units}", nameof(NotifyCombatInitializationRequired), message.CombatSeed, message.State.Units.Count, message.State.Units.Select(x => x.Id));

            await WaitWhileTrue(() => Game.Combat == null, "Combat has not been started on client yet. Waiting until start");

            Game.Combat.Seed = message.CombatSeed;
            Logger.LogInformation("Combat seed has been configured. Seed={Seed}", Game.Combat.Seed);

            var combatState = Mapper.Map<NetworkCombatState>(message.State);
            var areaEffects = Mapper.Map<List<NetworkAreaEffect>>(message.TriggeredAreaEffects);
            Game.Combat.TriggeredAreaEffects.AddRange(areaEffects);

            await CombatInteraction.UpdateCombatStateAsync(combatState, areaEffects, true);

            var confirmation = new ClientCombatInitializationCompleted();
            Logger.LogInformation("Sending {MessageType}", nameof(ClientCombatInitializationCompleted));
            Send(confirmation);
        }

        private void OnNotifyCombatInitializationCompleted(long playerId, NotifyCombatInitializationCompleted message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyCombatInitializationCompleted));
            SetCombatStage(NetworkCombatStage.Playing);
            Game.Combat.IsInitialized = true;
            Game.Combat.IsPlaying = true;
        }

        private async void OnNotifyDialogStarted(long playerId, NotifyDialogStarted started)
        {
            Logger.LogInformation("Received {MessageType}. DialogName={DialogName}, TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}", nameof(NotifyDialogStarted), started.DialogName, started.TargetUnitId, started.InitiatorUnitId);

            await WaitWhileTrue(() => Game.Dialog != null && Game.Dialog.IsSelectingAnswer, "Waiting until the previous answer has been processed");

            if (Game.Dialog == null || Game.Dialog.Name != started.DialogName)
            {
                Logger.LogInformation("New dialog has been initiated. PreviousDialogName={PreviousDialogName}, CurrentDialogName={CurrentDialogName}", Game.Dialog?.Name, started.DialogName);
                Game.Dialog = new NetworkDialog(started.DialogName);
            }

            var hasStartedDialog = await DialogInteraction.StartDialogAsync(started.DialogName, started.TargetUnitId, started.InitiatorUnitId, started.MapObjectId, started.SpeakerKey);
            if (!hasStartedDialog)
            {
                Logger.LogWarning("Client dialog is already started. DialogName={DialogName}", started.DialogName);
            }
        }

        private void OnNotifyDialogPopupClosed(long playerId, NotifyDialogPopupClosed dialogPopupClosed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", nameof(NotifyDialogPopupShown), playerId, dialogPopupClosed.Popup.AreaName, dialogPopupClosed.Popup.DialogName, dialogPopupClosed.Popup.CueName);
            var popup = Mapper.Map<NetworkDialogPopup>(dialogPopupClosed.Popup);

            DialogInteraction.CloseDialogPopup(popup);
        }

        private void OnNotifyDialogCueAnswerSelected(long playerId, NotifyDialogCueAnswerSelected selected)
        {
            Logger.LogInformation("Received {MessageType}. DialogName={DialogName}, CueName={CueName}, AnswerName={AnswerName}", nameof(NotifyDialogCueAnswerSelected), selected.DialogName, selected.CueName, selected.AnswerName);
            if (Game.Dialog == null)
            {
                Logger.LogError("Received dialog answer selection, but there is no active dialog right now. SuggestedDialogName={SuggestedDialogName}, SuggestedCueName={SuggestedCueName}, SuggestedAnswer={SuggestedAnswer}", selected.DialogName, selected.CueName, selected.AnswerName);
                return;
            }

            if (!string.Equals(Game.Dialog.Name, selected.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog answer selection has mismatched dialog name. SuggestedDialogName={SuggestedDialogName}, CurrentDialogName={CurrentDialogName}", selected.DialogName, Game.Dialog.Name);
                return;
            }

            if (!string.Equals(Game.Dialog.CurrentCueName, selected.CueName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog answer selection has mismatched cue. SuggestedCueName={SuggestedCueName}, CurrentCueName={CurrentCueName}", selected.CueName, Game.Dialog.CurrentCueName);
                return;
            }

            Game.Dialog.Answer = new NetworkDialogAnswer
            {
                AnswerName = selected.AnswerName,
                CueName = selected.CueName,
                ManualUnitSelectionId = selected.ManualUnitSelectionId,
            };

            Game.Dialog.AnswerSuggestions.Clear();
            DialogInteraction.SelectDialogAnswer(selected.AnswerName, selected.ManualUnitSelectionId);
        }

        private void OnNotifyDialogCueAnswerSuggested(long playerId, NotifyDialogCueAnswerSuggested suggested)
        {
            Logger.LogInformation("Received {MessageType}. DialogName={DialogName}, CueName={CueName}, Suggestions={Suggestions}", nameof(NotifyDialogCueAnswerSuggested), suggested.DialogName, suggested.CueName, suggested.Suggestions.Count);

            if (Game.Dialog == null)
            {
                Logger.LogError("Received dialog answer suggestion, but there is no active dialog right now. SuggestedDialogName={SuggestedDialogName}, SuggestedCueName={SuggestedCueName}, SuggestedAnswer={SuggestedAnswer}", suggested.DialogName, suggested.CueName);
                return;
            }

            if (!string.Equals(Game.Dialog.Name, suggested.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog suggestion has mismatched dialog name. SuggestedDialogName={SuggestedDialogName}, CurrentDialogName={CurrentDialogName}", suggested.DialogName, Game.Dialog.Name);
                return;
            }

            if (!string.Equals(Game.Dialog.CurrentCueName, suggested.CueName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog suggestion has mismatched dialog. SuggestedCueName={SuggestedCueName}, CurrentCueName={CurrentCueName}", suggested.CueName, Game.Dialog.CurrentCueName);
                return;
            }

            var suggestions = Mapper.Map<List<NetworkDialogAnswerSuggestion>>(suggested.Suggestions);
            DialogInteraction.MarkSuggestedDialogAnswers(suggestions);
        }

        private void OnNotifyPartyAreaTransitioned(long playerId, NotifyPartyAreaTransitioned partyLeftArea)
        {
            Logger.LogInformation("Received {MessageType}. AreaExitId={AreaExitId}, IsActionsTransition={IsActionsTransition}, FromAreaId={FromAreaId}, FromAreaName={FromAreaName}, ToAreaId={ToAreaId}, ToAreaName={ToAreaName}", nameof(NotifyPartyAreaTransitioned), partyLeftArea.Transition.AreaExitId, partyLeftArea.Transition.IsActionsTransition, partyLeftArea.Transition.From.Id, partyLeftArea.Transition.From.Name, partyLeftArea.Transition.To.Id, partyLeftArea.Transition.To.Name);

            var transition = Mapper.Map<NetworkAreaTransition>(partyLeftArea.Transition);
            GameInteraction.LeaveArea(transition);
        }

        private void OnNotifyGamePauseEnded(long playerId, NotifyGamePauseEnded message)
        {
            Logger.LogInformation("Received {MessageType}. AreaSeed={AreaSeed}, Party={Party}", nameof(NotifyGamePauseEnded), message.AreaSeed, message.Party);
            if (message.AreaSeed.HasValue)
            {
                SetAreaSeed(message.AreaSeed.Value);
            }

            var party = Mapper.Map<List<NetworkUnit>>(message.Party);
            CombatInteraction.UpdateUnits(party);
            Logger.LogInformation("Party units have been updated");

            Game.ForcedPause = null;
            GameInteraction.SetPause(false);
        }

        private void OnNotifyGameStarted(long playerId, NotifyGameStarted started)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGameStarted));

            if (Game.StartUp.IsNewGameSequence)
            {
                StartNewGameSequence();
                return;
            }

            if (string.IsNullOrEmpty(Game.StartUp?.SavePath))
            {
                Logger.LogCritical("Trying to start a game with missing save file path");
                return;
            }

            LoadSavedGame();
        }

        private void OnNotifyCharacterOwnerChanged(long playerId, NotifyCharacterOwnerChanged characterOwnerChanged)
        {
            Logger.LogInformation("Received {MessageType}. CharacterName={CharacterName}, CharacterId={CharacterId}, OwnerId={OwnerId}", nameof(NotifyCharacterOwnerChanged), characterOwnerChanged.Character.Name, characterOwnerChanged.Character.UnitId, characterOwnerChanged.Character.OwnerId);
            try
            {
                var newOwner = GetPlayer(characterOwnerChanged.Character.OwnerId);

                var character = Mapper.Map<NetworkCharacter>(characterOwnerChanged.Character);
                var actualCharacter = FindCharacter(character);
                if (actualCharacter == null)
                {
                    Logger.LogError("Unable to find character. CharacterName={CharacterName}, CharacterId={CharacterId}", character.Name, character.UnitId);
                    return;
                }
                actualCharacter.Owner = newOwner;
                if (!string.IsNullOrEmpty(character.UnitId))
                {
                    UpdateCharacterOwnershipHistory(character);
                }

                OnCharacterOwnerChanged?.Invoke(actualCharacter);

                UpdateInGameCharacterOwnershipChange(actualCharacter);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to handle changed character ownership");
                throw;
            }
        }

        private void OnNotifyLobbySaveGameChanged(long playerId, NotifyLobbySaveGameChanged notifyLobbySaveGameChanged)
        {
            Logger.LogInformation("Received {MessageType}. GameId={GameId}, Size={Size}, Seed={Seed}", nameof(NotifyLobbySaveGameChanged), notifyLobbySaveGameChanged.GameId, notifyLobbySaveGameChanged.Content?.Length, notifyLobbySaveGameChanged.Seed);

            UpdateSaveInfo(notifyLobbySaveGameChanged.GameId, notifyLobbySaveGameChanged.Content);

            Game.LoadedSaveSeed = notifyLobbySaveGameChanged.Seed;

            Logger.LogInformation("Game is ready to be started. SavePath={SavePath}, LoadedSaveSeed={LoadedSaveSeed}", Game.StartUp.SavePath, Game.LoadedSaveSeed);
            var confirmationMessage = new NotifyLobbySyncStatusChanged { PlayerId = Game.LocalPlayerId, Status = NetworkLobbySyncStatus.Succeed.ToString() };
            Send(confirmationMessage);
        }

        private void OnNotifyLobbyCharactersChanged(long playerId, NotifyLobbyCharactersChanged lobbyCharactersChanged)
        {
            Logger.LogInformation("Received {MessageType}. Portraits={Portraits}", nameof(NotifyLobbyCharactersChanged), string.Join(";", lobbyCharactersChanged.Characters.Select(c => c.Portrait)));

            Game.Characters.Clear();
            ResetCharacterOwnership();
            foreach (var networkCharacter in lobbyCharactersChanged.Characters)
            {
                var character = Mapper.Map<NetworkCharacter>(networkCharacter);
                character.Owner = GetPlayer(networkCharacter.OwnerId);
                Game.Characters.Add(character);
            }

            OnCharactersChanged?.Invoke(lobbyCharactersChanged.Title, Game.Characters);
        }

        private void OnNotifyLobbyPlayersChanged(long playerId, NotifyLobbyPlayersChanged playersChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayersCount={PlayersCount}", nameof(NotifyLobbyPlayersChanged), playersChanged.Players.Count);

            // a lot of lame lookups below, but shouldn't really matter for a small collection size
            var disconnectedPlayers = Game.Players.Where(x => !playersChanged.Players.Any(c => c.Id == x.Id)).ToList();
            var newPlayers = playersChanged.Players.Where(x => !Game.Players.Any(c => c.Id == x.Id)).ToList();
            // no need to handle player info updates here as any ready/loading/etc statuses are synced separately

            foreach (var disconnectedPlayer in disconnectedPlayers)
            {
                CleanupPlayer(disconnectedPlayer);
                ShowPlayerDisconnectedMessage(disconnectedPlayer);

                RefreshUIOnPlayerDisconnect(disconnectedPlayer.Id);
            }

            foreach (var newPlayer in newPlayers)
            {
                var player = Mapper.Map<NetworkPlayer>(newPlayer);
                Game.Players.Add(player);
                ShowPlayerConnectedMessage(player);
            }

            InvokeOnPlayersChanged();
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
            // should never happen?
            if (exception is not SocketException socketException)
            {
                Logger.LogError(exception, "Generic error occurred");
                InvokeOnNetworkError(WellKnownKeys.MultiplayerClient.Errors.GenericError.Key);
                return;
            }

            string error = string.Empty;
            SocketError? socketError = null;
            switch (socketException.SocketErrorCode)
            {
                case SocketError.OperationAborted: // client disconnected by a user
                    Logger.LogWarning("Skipping notification. SocketCode={SocketCode}", socketException.SocketErrorCode);
                    break;
                case SocketError.ConnectionReset:
                case SocketError.Success:
                    error = WellKnownKeys.MultiplayerClient.Errors.Disconnected.Key;
                    Game.Players.Clear();
                    UpdateRespecWindowStateOnPlayerLeave(Game.LocalPlayerId);
                    break;
                default:
                    socketError = socketException.SocketErrorCode;
                    error = WellKnownKeys.MultiplayerClient.Errors.NetworkError.Key;
                    break;
            }

            if (!string.IsNullOrEmpty(error))
            {
                InvokeOnNetworkError(error, socketError);
            }
        }

        private void OnGameServerConnectionSucceeded(long playerId, GameServerConnectionSucceeded connectionSucceeded)
        {
            Logger.LogInformation("Received {MessageType}. ClientPlayerId={ClientPlayerId}, SessionSeed={SessionSeed}", nameof(GameServerConnectionSucceeded), connectionSucceeded.ClientPlayerId, connectionSucceeded.SessionSeed);

            Game.LocalPlayerId = connectionSucceeded.ClientPlayerId;
            Game.SessionSeed = connectionSucceeded.SessionSeed;

            var settings = Mapper.Map<NetworkGameSettings>(connectionSucceeded.GameSettings);
            GameInteraction.ApplyGameSettings(settings);

            var contentState = GetInstalledContent();
            var message = new ClientGameServerConnectionConfirmed
            {
                PlayerName = SettingsService.GetSettings().PlayerName,
                ContentState = Mapper.Map<Networking.Messages.Contracts.NetworkContentState>(contentState)
            };

            Logger.LogInformation("Sending {MessageType}. PlayerName={PlayerName}, DLCsCount={DLCsCount}, ModsCount={ModsCount}", nameof(ClientGameServerConnectionConfirmed), message.PlayerName, message.ContentState.DLCs.Count, message.ContentState.Mods.Count);
            Send(message);
        }

        private void InvokeOnNetworkError(string error, SocketError? socketError = null)
        {
            OnNetworkError?.Invoke();
            PlayerNotification.ShowModalMessage(error, socketError);
        }
    }
}
