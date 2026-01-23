using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using AutoMapper;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Area;
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

        public void OnAreaLoadingComplete()
        {
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
                    EnsureForcePaused(WellKnownKeys.GameNotifications.ForcedPause.RestRandomEncounterLoading.Key);
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
                if (aiActions == null || string.IsNullOrEmpty(networkAIAction.ActionBlueprintId))
                {
                    return null;
                }

                var message = new AIActionRequest
                {
                    Timeout = TimeSpan.FromSeconds(3),
                    UnitId = networkAIAction.UnitId,
                    ActionIndex = aiActions.Count
                };

                Logger.LogInformation("Retrieving AI action. UnitId={UnitId}, ActionIndex={ActionIndex}", networkAIAction.UnitId, message.ActionIndex);

                var response = _networkClient.SendAndWaitForAsync<AIActionResponse>(message).Result;

                if (response?.Action == null)
                {
                    Logger.LogWarning("Host has no next action for current unit. UnitId={UnitId}", networkAIAction.UnitId);
                    return null;
                }

                var action = Mapper.Map<NetworkAIAction>(response.Action);

                if (string.Equals(action.ActionBlueprintId, networkAIAction.ActionBlueprintId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(action.TargetId, networkAIAction.TargetId, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInformation("Host AI action is the same, nothing to do here. UnitId={UnitId}, ActionBlueprintId={ActionBlueprintId}, TargetUnitId={TargetUnitId}", networkAIAction.UnitId, networkAIAction.ActionBlueprintId, networkAIAction.TargetId);
                    aiActions.Add(networkAIAction);
                    return null;
                }

                aiActions.Add(action);
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

        public bool TogglePause(bool isPaused)
        {
            if (Game.ForcedPause != null && isPaused)
            {
                var warningText = string.IsNullOrEmpty(Game.ForcedPause.Reason) ? WellKnownKeys.GameNotifications.ForcedPause.NoPermission.Key : Game.ForcedPause.Reason;
                PlayerNotification.ShowWarningNotification(warningText);
            }

            // client has no control over manual pausing at all
            return false;
        }

        public void OnAutoPausedByTrapDetection()
        {
            var message = new ClientGameAutoPaused();
            Send(message);
            // client doesn't care about exact reason because of no permissions to unpause it anyway
            EnsureForcePaused(reason: null, removalDelay: null);
        }

        public bool OnClickGroupChangerUnit(string unitId)
        {
            // client is not allowed to move characters (no restrictions, just to avoid implementing extra synchronization)
            return false;
        }

        public bool OnGlobalMapSelectLocation(NetworkGlobalMapLocation globalMapLocation)
        {
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
            var isInParty = IsControlledByPlayers(polymorphicItem.UnitId);
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
               .On<NotifyGamePauseStarted>(OnNotifyGamePauseStarted)
               .On<NotifyGamePauseEnded>(OnNotifyGamePauseEnded)

               // area transitioning
               .On<NotifyPartyAreaTransitioned>(OnNotifyPartyAreaTransitioned)

               // leveling
               .On<NotifyCharacterLevelingStarted>(OnNotifyCharacterLevelingStarted)

               // character selection window
               .On<NotifyCharacterSelectionToggleChanged>(OnNotifyCharacterSelectionToggleChanged)
               .On<NotifyCharacterSelectionWindowAccepted>(OnNotifyCharacterSelectionWindowAccepted)
               .On<NotifyCharacterSelectionWindowClosed>(OnNotifyCharacterSelectionWindowClosed)

               // rest
               .On<NotifyRestStarted>(OnNotifyRestStarted)
               .On<NotifySpawnCampPlace>(OnNotifySpawnCampPlace)
               .On<NotifyCampingUseHealingSpellsChanged>(OnNotifyCampingUseHealingSpellsChanged)
               .On<NotifyCampingStateChanged>(OnNotifyCampingStateChanged)
               .On<NotifyCampingUnitsRoleChanged>(OnNotifyCampingUnitsRoleChanged)

               // combat
               .On<NotifyInvalidCombatTurnStarted>(OnNotifyInvalidCombatTurnStarted)
               .On<NotifyCombatInitialized>(OnNotifyCombatInitialized)
               .On<NotifyCombatTurnStarted>(OnNotifyCombatTurnStarted)
               .On<NotifyCombatTurnSynchronizationRequired>(OnNotifyCombatTurnSynchronizationRequired)

               // global map & crusade combat
               .On<NotifyGlobalMapRestMenuOpened>(OnNotifyGlobalMapRestMenuOpened)
               .On<NotifyGlobalMapTravelStarted>(OnNotifyGlobalMapTravelStarted)
               .On<NotifyGlobalMapTravelStopped>(OnNotifyGlobalMapTravelStopped)
               .On<NotifyGlobalMapTravelContinued>(OnNotifyGlobalMapTravelContinued)
               .On<NotifyGlobalMapCommonPopupAccepted>(OnNotifyGlobalMapIngredientCollectionAccepted)
               .On<NotifyGlobalMapLocationEntered>(OnNotifyGlobalMapLocationEntered)
               .On<NotifyGlobalMapEncounterAccepted>(OnNotifyGlobalMapEncounterAccepted)
               .On<NotifyGlobalMapEncounterAvoided>(OnNotifyGlobalMapEncounterAvoided)
               .On<NotifyGlobalMapEncounterRolled>(OnNotifyGlobalMapEncounterRolled)
               .On<NotifyGlobalMapLocationMessageClosed>(OnNotifyGlobalMapLocationMessageClosed)
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
               .On<NotifyGlobalMapCrusadeArmyDismissed>(OnNotifyGlobalMapCrusadeArmyDismissed)
               .On<NotifyGlobalMapCrusadeArmyInfoClosed>(OnNotifyGlobalMapCrusadeArmyInfoClosed)
               .On<NotifyGlobalMapCrusadeArmyInfoShown>(OnNotifyGlobalMapCrusadeArmyInfoShown)
               .On<NotifyGlobalMapCrusadeArmySquadsMovedToMainArmy>(OnNotifyGlobalMapCrusadeArmySquadsMovedToMainArmy)
               .On<NotifyGlobalMapCrusadeArmySquadsMovedToSecondArmy>(OnNotifyGlobalMapCrusadeArmySquadsMovedToSecondArmy)
               .On<NotifyGlobalMapCrusadeArmyInfoMergeClosed>(OnNotifyGlobalMapCrusadeArmyInfoMergeClosed)
               .On<NotifyGlobalMapCrusadeArmyInfoNextMergeArmySelected>(OnNotifyGlobalMapCrusadeArmyInfoNextMergeArmySelected)
               .On<NotifyGlobalMapCrusadeArmyInfoPrevMergeArmySelected>(OnNotifyGlobalMapCrusadeArmyInfoPrevMergeArmySelected)
               .On<NotifyGlobalMapCrusadeArmyLeaderActionExecuted>(OnNotifyGlobalMapCrusadeArmyLeaderActionExecuted)
               .On<NotifyGlobalMapCrusadeArmiesMerging>(OnNotifyGlobalMapCrusadeArmiesMerging)
               .On<NotifyGlobalMapCrusadeArmyInfoArmyCreated>(OnNotifyGlobalMapCrusadeArmyInfoArmyCreated)
               .On<NotifyGlobalMapCrusadeArmyInfoMainClosed>(OnNotifyGlobalMapCrusadeArmyInfoMainClosed)
               .On<NotifyGlobalMapCrusadeArmyInfoMainNameChanged>(OnNotifyGlobalMapCrusadeArmyInfoMainNameChanged)
               .On<NotifyGlobalMapCrusadeArmyInfoMergeNameChanged>(OnNotifyGlobalMapCrusadeArmyInfoMergeNameChanged)
               .On<NotifyGlobalMapCrusadeArmySetLeaderClosed>(OnNotifyGlobalMapCrusadeArmySetLeaderClosed)
               .On<NotifyGlobalMapCrusadeArmySetLeaderClearClicked>(OnNotifyGlobalMapCrusadeArmySetLeaderCleared)
               .On<NotifyGlobalMapCrusadeArmySetLeaderRecruitClicked>(OnNotifyGlobalMapCrusadeArmySetLeaderRecruitClicked)
               .On<NotifyGlobalMapCrusadeArmyBuyLeaderClosed>(OnNotifyGlobalMapCrusadeArmyBuyLeaderClosed)

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
               .On<NotifyZoneLootRemoveToggleChanged>(OnNotifyZoneLootRemoveToggleChanged)

               // inventory
               .On<NotifyPolymorphicItemCreated>(OnNotifyPolymorphicItemCreated)
               ;
        }

        private void OnNotifyGlobalMapCrusadeArmyBuyLeaderClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyBuyLeaderClosed globalMapCrusadeArmyBuyLeaderClosed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapTravelerModeChanged), globalMapCrusadeArmyBuyLeaderClosed.PlayerId);
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyBuyLeader, globalMapCrusadeArmyBuyLeaderClosed.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateAfterBuyLeader();

            GlobalMapInteraction.CloseBuyLeaderScreen();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCrusadeArmyBuyLeaderClosed);
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderRecruitClicked(long arg1, NotifyGlobalMapCrusadeArmySetLeaderRecruitClicked globalMapCrusadeArmySetLeaderRecruitClicked)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmySetLeaderRecruitClicked));

            GlobalMapInteraction.ClickRecruitmentOnSetLeaderScreen();
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderCleared(long receivedFrom, NotifyGlobalMapCrusadeArmySetLeaderClearClicked globalMapCrusadeArmyInfoSetLeaderCleared)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmySetLeaderClearClicked));

            GlobalMapInteraction.ClearLeaderOnCrusdeArmyInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderClosed(long receivedFrom, NotifyGlobalMapCrusadeArmySetLeaderClosed globalMapCrusadeArmyInfoSetLeaderClosed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapTravelerModeChanged), globalMapCrusadeArmyInfoSetLeaderClosed.PlayerId);
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmySetLeader, globalMapCrusadeArmyInfoSetLeaderClosed.PlayerId);

            UpdateGlobalMapCrusadeArmyInfoUIStateAfterSetLeader();

            GlobalMapInteraction.CloseCrusadeArmySetLeaderInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoMergeNameChanged(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoMergeNameChanged message)
        {
            Logger.LogInformation("Received {MessageType}. ArmyId={ArmyId}, Name={Name}", nameof(NotifyGlobalMapCrusadeArmyInfoMainNameChanged), message.Army.Id, message.Army.Name);

            var army = Mapper.Map<NetworkGlobalMapArmy>(message.Army);

            GlobalMapInteraction.SetCrusadeArmyInfoMergeName(army);
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoMainNameChanged(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoMainNameChanged message)
        {
            Logger.LogInformation("Received {MessageType}. ArmyId={ArmyId}, Name={Name}", nameof(NotifyGlobalMapCrusadeArmyInfoMainNameChanged), message.Army.Id, message.Army.Name);

            var army = Mapper.Map<NetworkGlobalMapArmy>(message.Army);

            GlobalMapInteraction.SetCrusadeArmyInfoMainName(army);
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoMainClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoMainClosed message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmyInfoMainClosed));

            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyInfo);

            GlobalMapInteraction.CloseCrusadeArmyMainInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoArmyCreated(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoArmyCreated message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapCrusadeArmyInfoArmyCreated));

            GlobalMapInteraction.CreateArmyAtCrusadeArmyInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoShown(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoShown message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapCombatResultsShown), receivedFrom, message.PlayerId);
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

        private void OnNotifyGlobalMapCrusadeArmyInfoMergeClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoMergeClosed globalMapCrusadeArmyInfoMergeClosed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapTravelerModeChanged), receivedFrom, globalMapCrusadeArmyInfoMergeClosed.PlayerId);
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

        private void OnNotifyGlobalMapCrusadeArmyDismissed(long receivedFrom, NotifyGlobalMapCrusadeArmyDismissed crusadeArmyDismissed)
        {
            Logger.LogInformation("Received {MessageType}. ArmyId={SourceArmyId}", nameof(NotifyGlobalMapCrusadeArmyDismissed), crusadeArmyDismissed.Army.Id);
            var army = Mapper.Map<NetworkGlobalMapArmy>(crusadeArmyDismissed.Army);

            GlobalMapInteraction.DismissCrusadeArmy(army);
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
            Logger.LogInformation("Received {MessageType}. UnitId={UnitId}, AbilityId={AbilityId}, Path={Path}", nameof(NotifyTacticalUnitUseAbilityCommandExecuted), tacticalUnitUseAbilityCommandExecuted.Command.Ability.CasterId, tacticalUnitUseAbilityCommandExecuted.Command.Ability.Id, tacticalUnitUseAbilityCommandExecuted.Command.Ability.VectorPath);
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
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, AreaSeed={AreaSeed}, Seed={Seed}", nameof(NotifyTacticalCombatInitialized), receivedFrom, tacticalCombatInitialized.AreaSeed, tacticalCombatInitialized.AreaSeed);

            await WaitWhileTrue(() => Game.ArmyCombat == null, "Crusade army combat has not been started yet");

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

        private void OnNotifyGlobalMapCommonPopupDeclined(long receivedFrom, NotifyGlobalMapCommonPopupDeclined globalMapCommonPopupDeclined)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Type={Type}, LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapCommonPopupDeclined), globalMapCommonPopupDeclined.PlayerId, globalMapCommonPopupDeclined.Popup.Type, globalMapCommonPopupDeclined.Popup.Location?.Id, globalMapCommonPopupDeclined.Popup.Location?.Name);

            RemovePlayerFromTracker(Game.PlayersInGlobalMapCommonPopup, globalMapCommonPopupDeclined.PlayerId);

            GlobalMapInteraction.DeclineCommonPopup();
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

        private void OnNotifyZoneLootRemoveToggleChanged(long playerId, NotifyZoneLootRemoveToggleChanged zoneLootRemoveToggleChanged)
        {
            Logger.LogInformation("Received {MessageType}. RemoveLoot={RemoveLoot}", nameof(NotifyZoneLootRemoveToggleChanged), zoneLootRemoveToggleChanged.RemoveLoot);

            GameInteraction.UpdateZoneLootRemoveToggle(zoneLootRemoveToggleChanged.RemoveLoot);
        }

        private void OnNotifyZoneLootCompleted(long playerId, NotifyZoneLootCompleted zoneLootCompleted)
        {
            Logger.LogInformation("Received {MessageType}. RemoveLoot={RemoveLoot}", nameof(NotifyZoneLootCompleted));

            GameInteraction.CompleteZoneLoot();
        }

        private void OnNotifyGlobalMapEncounterRolled(long playerId, NotifyGlobalMapEncounterRolled globalMapEncounterRolled)
        {
            Logger.LogInformation("Sending {MessageType}. Seed={Seed}, EncounterId={EncounterId}, Position={Position}, Avoidance={Avoidance}", nameof(NotifyGlobalMapEncounterRolled), globalMapEncounterRolled.Encounter.Seed, globalMapEncounterRolled.Encounter.BlueprintId, globalMapEncounterRolled.Encounter.Position, globalMapEncounterRolled.Encounter.AvoidanceResult);
            var encounter = Mapper.Map<NetworkGlobalMapEncounter>(globalMapEncounterRolled.Encounter);

            GlobalMapInteraction.RollEncounter(encounter);
        }

        private void OnNotifyGlobalMapEncounterAvoided(long playerId, NotifyGlobalMapEncounterAvoided globalMapEncounterAvoided)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapLocationEntered));
            GlobalMapInteraction.AvoidEncounter();

            ResetPlayersTracker(Game.PlayersInGlobalMapEncounterMessage);
        }

        private void OnNotifyGlobalMapEncounterAccepted(long playerId, NotifyGlobalMapEncounterAccepted notifyGlobalMapEncounterAccepted)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapEncounterAccepted));
            GlobalMapInteraction.AcceptEncounter();

            ResetPlayersTracker(Game.PlayersInGlobalMapEncounterMessage);
        }

        private void OnNotifyGlobalMapLocationEntered(long playerId, NotifyGlobalMapLocationEntered globalMapLocationEntered)
        {
            Logger.LogInformation("Received {MessageType}. LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapLocationEntered), globalMapLocationEntered.Location.Id, globalMapLocationEntered.Location.Name);

            var location = Mapper.Map<NetworkGlobalMapLocation>(globalMapLocationEntered.Location);
            GlobalMapInteraction.EnterLocation(location);

            ResetPlayersTracker(Game.PlayersInGlobalMapLocationMessage);
        }

        private void OnNotifyGlobalMapIngredientCollectionAccepted(long playerId, NotifyGlobalMapCommonPopupAccepted globalMapCommonPopupAccepted)
        {
            Logger.LogInformation("Received {MessageType}. Type={Type}, LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapCommonPopupAccepted), globalMapCommonPopupAccepted.Popup.Type, globalMapCommonPopupAccepted.Popup.Location?.Id, globalMapCommonPopupAccepted.Popup.Location?.Name);

            var popup = Mapper.Map<NetworkGlobalMapCommonPopup>(globalMapCommonPopupAccepted.Popup);
            GlobalMapInteraction.AcceptCommonPopup(popup);

            ResetPlayersTracker(Game.PlayersInGlobalMapCommonPopup);
        }

        private void OnNotifyGlobalMapTravelContinued(long playerId, NotifyGlobalMapTravelContinued globalMapTravelContinued)
        {
            Logger.LogInformation("Received {MessageType}. EdgePosition={EdgePosition}", nameof(NotifyGlobalMapTravelContinued), globalMapTravelContinued.Traveler.Position?.EdgePosition);
            var traveler = Mapper.Map<NetworkGlobalMapTraveler>(globalMapTravelContinued.Traveler);
            GlobalMapInteraction.ContinueTravel(traveler);
        }

        private void OnNotifyGlobalMapTravelStopped(long playerId, NotifyGlobalMapTravelStopped globalMapTravelStopped)
        {
            Logger.LogInformation("Received {MessageType}. EdgePosition={EdgePosition}", nameof(NotifyGlobalMapTravelStopped), globalMapTravelStopped.Traveler.Position?.EdgePosition);

            var traveler = Mapper.Map<NetworkGlobalMapTraveler>(globalMapTravelStopped.Traveler);
            GlobalMapInteraction.StopTravel(traveler);
        }

        private void OnNotifySkipTimeStarted(long playerId, NotifySkipTimeStarted skipTimeStarted)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifySkipTimeStarted));
            ResetPlayersTracker(Game.PlayersInSkipTime);
            GameInteraction.StartSkipTime();
        }

        private void OnNotifySkipTimeHoursChanged(long playerId, NotifySkipTimeHoursChanged skipTimeHoursChanged)
        {
            Logger.LogInformation("Received {MessageType}. Hours={Hours}", nameof(NotifySkipTimeHoursChanged), skipTimeHoursChanged.Hours);
            GameInteraction.UpdateSkipTimeHours(skipTimeHoursChanged.Hours);
        }

        private void OnNotifySkipTimeClosed(long playerId, NotifySkipTimeClosed skipTimeClosed)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifySkipTimeOpened));
            GameInteraction.CloseSkipTimeUI();
            ResetPlayersTracker(Game.PlayersInSkipTime);
        }

        private void OnNotifyGlobalMapTravelStarted(long playerId, NotifyGlobalMapTravelStarted globalMapTravelStarted)
        {
            Logger.LogInformation("Received {MessageType}. Type={Type}, MovementPoints={MovementPoints}, FromClick={FromClick}, DestinationId={DestinationId}, DestinationName={DestinationName}", nameof(NotifyGlobalMapTravelStarted), globalMapTravelStarted.Travel.Type, globalMapTravelStarted.Travel.Traveler.MovementPoints, globalMapTravelStarted.Travel.FromClick, globalMapTravelStarted.Travel.Destination.Id, globalMapTravelStarted.Travel.Destination.Name);

            var travel = Mapper.Map<NetworkGlobalMapTravel>(globalMapTravelStarted.Travel);

            GlobalMapInteraction.StartTravel(travel);
        }

        private void OnNotifyGlobalMapRestMenuOpened(long playerId, NotifyGlobalMapRestMenuOpened globalMapRestMenuOpened)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapRestMenuOpened));
            GlobalMapInteraction.OpenRestMenu();
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

        private void OnNotifyGamePauseStarted(long playerId, NotifyGamePauseStarted pauseStarted)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGamePauseStarted));
            EnsureForcePaused(null);
            GameInteraction.SetPause(true);
        }

        private void OnNotifyCharacterLevelingStarted(long playerId, NotifyCharacterLevelingStarted characterLevelingStarted)
        {
            Logger.LogInformation("Received {MessageType}. UnitId={unitId}, Type={Type}", nameof(NotifyCharacterLevelingStarted), characterLevelingStarted.UnitId, characterLevelingStarted.Type);

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

        private void OnNotifyInvalidCombatTurnStarted(long playerId, NotifyInvalidCombatTurnStarted started)
        {
            Logger.LogInformation("Received {MessageType}. UnitId={UnitId}", nameof(NotifyInvalidCombatTurnStarted), started.UnitId);
            PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.ClientTurnOrderDesync.Key);
            ResetCombatTurn();
            CombatInteraction.StartTurnBasedCombatTurn(started.UnitId);
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

        private async void OnNotifyCombatTurnSynchronizationRequired(long playerId, NotifyCombatTurnSynchronizationRequired combatTurnSynchronization)
        {
            try
            {
                Logger.LogInformation("Received {MessageType}. Units={Units}, UnitTurn={UnitTurn}", nameof(NotifyCombatTurnSynchronizationRequired), combatTurnSynchronization.CombatState.Units.Count, combatTurnSynchronization.UnitId);

                var unitId = Game.Combat.Turn.UnitId;
                if (!string.Equals(unitId, combatTurnSynchronization.UnitId, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("Synchronization request contains mismatched unit. LocalUnitId={LocalUnitId}, RemoteRound={RemoteRound}, RemoteUnitId={RemoteUnitId}", unitId, combatTurnSynchronization.UnitId);
                    return;
                }

                var combatState = Mapper.Map<NetworkCombatState>(combatTurnSynchronization.CombatState);
                await CombatInteraction.UpdateCombatStateAsync(combatState, false);

                var message = new ClientCombatTurnSynchronized { UnitId = unitId };
                Logger.LogInformation("Units have been synchronized. Sending {MessageType} confirmation. UnitId={UnitId}", nameof(NotifyCombatTurnSynchronizationRequired), unitId);
                Send(message);
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

        private void OnNotifyCombatTurnStarted(long playerId, NotifyCombatTurnStarted started)
        {
            Logger.LogInformation("Received {MessageType}. Round={Round}, UnitId={UnitId}", nameof(NotifyCombatTurnStarted), started.Round, started.UnitId);
            if (Game.Combat?.Turn == null)
            {
                Logger.LogError("Trying to start not initialized turn. Round={Round}, UnitId={UnitId}", started.Round, started.UnitId);
                return;
            }

            if (!string.Equals(started.UnitId, Game.Combat.Turn.UnitId, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning("Starting turn with different UnitId. LocalUnitId={LocalUnitId}, HostUnitId={HostUnitId}", Game.Combat.Turn.UnitId, started.UnitId);
            }

            if (Game.Combat.Round != started.Round)
            {
                Logger.LogWarning("Starting turn with different Round number. LocalRound={LocalRound}, HostRound={HostRound}", Game.Combat.Round, started.Round);
            }

            Game.Combat.Turn.IsInProgress = true;
            CombatInteraction.StartTurnBasedCombatTurn(Game.Combat.Turn.UnitId);
        }

        private async void OnNotifyCombatInitialized(long playerId, NotifyCombatInitialized combatInitialized)
        {
            Logger.LogInformation("Received {MessageType}. Seed={Seed}, Units={Units}", nameof(NotifyCombatInitialized), combatInitialized.Seed, combatInitialized.CombatState.Units.Count);

            await WaitWhileTrue(() => Game.Combat == null, "Combat has not been started on client yet. Waiting until start");

            Game.Combat.Seed = combatInitialized.Seed;
            Logger.LogInformation("Combat seed has been configured. Seed={Seed}", Game.Combat.Seed);

            var combatState = Mapper.Map<NetworkCombatState>(combatInitialized.CombatState);
            await CombatInteraction.UpdateCombatStateAsync(combatState, true);

            Logger.LogInformation("Sending {MessageType}", nameof(ClientCombatInitialized));
            var message = new ClientCombatInitialized();
            Send(message);

            Game.Combat.IsInitialized = true;
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

        private void OnNotifyGamePauseEnded(long playerId, NotifyGamePauseEnded changed)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGamePauseEnded));
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
            Logger.LogInformation("Received {MessageType}. GameId={GameId}, Size={Size}", nameof(NotifyLobbySaveGameChanged), notifyLobbySaveGameChanged.GameId, notifyLobbySaveGameChanged.Content?.Length);

            UpdateSaveInfo(notifyLobbySaveGameChanged.GameId, notifyLobbySaveGameChanged.Content);

            Logger.LogInformation("Game is ready to be started. SavePath={SavePath}", Game.StartUp.SavePath);
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

            OnCharactersChanged?.Invoke(Game.Characters);
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
                    UpdateRespecWindowStateOnPlayerLeave(GetLocalPlayerId());
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
