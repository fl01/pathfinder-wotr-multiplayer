using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AutoMapper;
using Kingmaker.UI.Kingdom;
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
using WOTRMultiplayer.Entities.GlobalMap.Kingdom;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Entities.NewGame;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Entities.Units;
using WOTRMultiplayer.Logging.Extensions;
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

        public override void OnAreaLoaded()
        {
            base.OnAreaLoaded();

            var message = new ClientAreaLoaded();
            Send(message);
        }

        public void OnAfterCueShow(NetworkDialog networkDialog, string cueName, bool hasSystemAnswer)
        {
            Logger.LogInformation("Showing dialog Cue. DialogId={DialogId}, DialogName={DialogName}, CueName={CueName}, HasSystemAnswer={HasSystemAnswer}", networkDialog.Id, networkDialog.Name, cueName, hasSystemAnswer);
            if (hasSystemAnswer)
            {
                DialogInteraction.SetDialogContinueButtonState(false);
            }

            Game.DialogState.CurrentCueName = cueName;
            Game.DialogState.Answer = null;

            var message = new ClientDialogCueWitnessed
            {
                CueName = cueName,
                Dialog = Mapper.Map<Networking.Messages.Contracts.NetworkDialog>(networkDialog)
            };
            Send(message);
        }

        public bool OnBeforeSelectDialogAnswer(NetworkDialog networkDialog, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId)
        {
            if (Game.DialogState == null)
            {
                Logger.LogError("Current dialog is null");
                return false;
            }

            if (Game.DialogState.Answer != null && string.Equals(answerName, Game.DialogState.Answer.AnswerName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Proceeding with dialog answer without extra steps. DialogId={DialogId}, DialogName={DialogName}, CueName={CueName}, AnswerName={AnswerName}", Game.DialogState.Dialog.Id, Game.DialogState.Dialog.Name, cueName, answerName);
                Game.DialogState.IsSelectingAnswer = false;
                return true;
            }

            var message = new ClientDialogCueAnswerSuggested
            {
                Dialog = Mapper.Map<Networking.Messages.Contracts.NetworkDialog>(networkDialog),
                CueName = cueName,
                AnswerName = answerName
            };
            Send(message);

            Game.DialogState.IsSelectingAnswer = true;
            return false;
        }

        public bool StartDialog(NetworkDialog networkDialog)
        {
            if (networkDialog.IsScripted)
            {
                Game.DialogState = new NetworkDialogState(networkDialog);
                Logger.LogInformation("Scripted dialog has been started. DialogId={DialogId}, DialogName={Name}", Game.DialogState.Dialog.Id, Game.DialogState.Dialog.Name);
                return true;
            }

            if (string.Equals(Game.DialogState?.Dialog.Id, networkDialog.Id, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Dialog has been initiated, proceeding with default game logic. Id={Id}, Name={Name}", networkDialog.Id, networkDialog.Name);
                return true;
            }

            var message = new ClientDialogStartRequested
            {
                Dialog = Mapper.Map<Networking.Messages.Contracts.NetworkDialog>(networkDialog)
            };
            Send(message);

            return false;
        }

        public bool CanInitializeCombat()
        {
            if (Game.Combat == null)
            {
                return false;
            }

            try
            {
                if (!Game.Combat.IsPrepared)
                {
                    var units = CombatInteraction.GetUnitsInCombat();
                    var message = new ClientCombatPreparationStarted
                    {
                        Units = Mapper.Map<List<Networking.Messages.Contracts.NetworkUnit>>(units),
                    };
                    Send(message);
                    Game.Combat.IsPrepared = true;
                }

                return Game.Combat.IsInitialized;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while initializing combat");
                throw;
            }
        }

        public bool CanContinueCombat()
        {
            var canContinueCombat = Game.Combat != null && Game.Combat.IsPlaying;
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
                Send(message);
                return true;
            }

            ResetPlayersTracker(Game.PlayersInGlobalMapCombatResults);
            Game.ArmyCombat = new NetworkArmyCombat() { IsInitialized = false };

            return false;
        }

        public NetworkAIAction OnAfterAISelectedAction(NetworkAIAction networkAIAction)
        {
            try
            {
                // crusade combat is fine as of now
                if (Game.ArmyCombat != null)
                {
                    return null;
                }

                if (Game.Combat?.Turn == null)
                {
                    return null;
                }

                var action = Game.Combat.Turn.AIActions.FirstOrDefault(a => string.Equals(a.UnitId, networkAIAction.UnitId)
                    && string.Equals(a.Id, networkAIAction.Id, StringComparison.OrdinalIgnoreCase));

                if (action == null && networkAIAction.IsAbility)
                {
                    var firstDifferentAction = Game.Combat.Turn.AIActions
                        .OrderByDescending(x => x.IsAbility)
                        .FirstOrDefault(a => !string.Equals(a.Id, networkAIAction.Id, StringComparison.OrdinalIgnoreCase));

                    // try to use another action (preferrably ability)
                    Logger.LogWarning("Requested AI action has not been found within existing actions. UnitId={UnitId}, ActionId={ActionId}, ActionName={ActionName}, FallbackActionId={FallbackActionId}, FallbackActionName={FallbackActionName}",
                        networkAIAction.UnitId, networkAIAction.Id, networkAIAction.Name, firstDifferentAction?.Id, firstDifferentAction?.Name);
                    return firstDifferentAction;
                }

                return action;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while selecting AI action");
                return null;
            }
        }

        protected override DiceRollValueResponse RetrieveRoll(DiceRollValueRequest rollRequest)
        {
            return _networkClient.SendAndWaitForAsync<DiceRollValueResponse>(rollRequest).Result;
        }

        protected override void Send(object message)
        {
            Logger.LogObject(LogLevel.Information, "Sending {MessageType}.", message);
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
            return false;
        }

        protected override void OnLocalPlayerTurnEnd()
        {
            base.OnLocalPlayerTurnEnd();

            if (Game.Combat.Turn.AIActions.Count > 0)
            {
                Game.Combat.Turn.AIActions.Clear();
                Logger.LogInformation("AI actions have been cleared");
            }
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
               .On<NotifyCombatTurnStartSynchronizationRequired>(OnNotifyCombatTurnStartSynchronizationRequired)
               .On<NotifyCombatTurnEndSynchronizationRequired>(OnNotifyCombatTurnEndSynchronizationRequired)
               .On<NotifyCombatTurnStarted>(OnNotifyCombatTurnStarted)
               .On<NotifyAIActionSelected>(OnNotifyAIActionExecuted)
               .On<NotifyCombatRecoveryRequired>(OnNotifyCombatRecoveryRequired)
               .On<NotifyCombatLocalTurnEnded>(OnNotifyCombatLocalTurnEnded)
               .On<NotifyCombatTurnEnded>(OnNotifyCombatTurnEnded)

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
               .On<NotifyGlobalMapTeleport>(OnNotifyGlobalMapTeleport)

               // kingdom
               .On<NotifyKingdomNavigationChanged>(OnNotifyKingdomNavigationChanged)
               .On<NotifyKingdomEventSelected>(OnNotifyKingdomEventSelected)
               .On<NotifyKingdomEventSolutionSelected>(OnNotifyKingdomEventSolutionSelected)
               .On<NotifyKingdomEventStarted>(OnNotifyKingdomEventStarted)
               .On<NotifyKingdomEventCancelled>(OnNotifyKingdomEventCancelled)
               .On<NotifyKingdomEventDropped>(OnNotifyKingdomEventDropped)
               .On<NotifyKingdomSettlementEntered>(OnNotifyKingdomSettlementEntered)
               .On<NotifyKingdomSettlementLeft>(OnNotifyKingdomSettlementLeft)
               .On<NotifyKingdomSettlementBuildingSold>(OnNotifyKingdomSettlementBuildingSold)
               .On<NotifyKingdomSettlementBuildingBuilt>(OnNotifyKingdomSettlementBuildingBuilt)

               // dialogs
               .On<NotifyDialogStarted>(OnNotifyDialogStarted)
               .On<NotifyDialogCueAnswerSuggested>(OnNotifyDialogCueAnswerSuggested)
               .On<NotifyDialogCueAnswerSelected>(OnNotifyDialogCueAnswerSelected)
               .On<NotifyDialogPopupClosed>(OnNotifyDialogPopupClosed)
               .On<NotifyDialogPopupAccepted>(OnNotifyDialogPopupAccepted)

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

               // map objects
               .On<NotifyTransitionMapEntryChosen>(OnNotifyTransitionMapEntryChosen)
               .On<NotifyTransitionMapClosed>(OnNotifyTransitionMapClosed)

               // inventory
               .On<NotifyPolymorphicItemCreated>(OnNotifyPolymorphicItemCreated)
               ;
        }

        private void OnNotifyGlobalMapTeleport(long receivedFrom, NotifyGlobalMapTeleport message)
        {
            var location = Mapper.Map<NetworkGlobalMapLocation>(message.Location);
            GlobalMapInteraction.Teleport(location);
        }

        private void OnNotifyTransitionMapEntryChosen(long receivedFrom, NotifyTransitionMapEntryChosen message)
        {
            ResetPlayersTracker(Game.PlayersInTransitionMap);
            GameInteraction.ChooseTransitionMapEntry(message.EntryId);
        }

        private void OnNotifyTransitionMapClosed(long receivedFrom, NotifyTransitionMapClosed message)
        {
            ResetPlayersTracker(Game.PlayersInTransitionMap);
            GameInteraction.CloseTransitionMap();
        }

        private void OnNotifyKingdomSettlementBuildingBuilt(long receivedFrom, NotifyKingdomSettlementBuildingBuilt message)
        {
            var building = Mapper.Map<NetworkKingdomSettlementBuilding>(message.Building);
            GlobalMapInteraction.BuildSettlementBuilding(building);
        }

        private void OnNotifyKingdomSettlementBuildingSold(long receivedFrom, NotifyKingdomSettlementBuildingSold message)
        {
            var building = Mapper.Map<NetworkKingdomSettlementBuilding>(message.Building);
            GlobalMapInteraction.SellSettlementBuilding(building);
        }

        private void OnNotifyKingdomSettlementLeft(long receivedFrom, NotifyKingdomSettlementLeft message)
        {
            GlobalMapInteraction.LeaveSettlement();
        }

        private void OnNotifyKingdomSettlementEntered(long receivedFrom, NotifyKingdomSettlementEntered message)
        {
            var settlement = Mapper.Map<NetworkKingdomSettlement>(message.Settlement);
            GlobalMapInteraction.EnterSettlement(settlement, message.RequiresUnloadEvent, message.ExitSettlementToGlobalMap);
        }

        private void OnNotifyKingdomEventDropped(long receivedFrom, NotifyKingdomEventDropped message)
        {
            var kingdomEvent = Mapper.Map<NetworkKingdomEvent>(message.Event);
            GlobalMapInteraction.DropKingdomEvent(kingdomEvent);
        }

        private void OnNotifyKingdomEventCancelled(long receivedFrom, NotifyKingdomEventCancelled message)
        {
            GlobalMapInteraction.CancelKingdomEvent();
        }

        private void OnNotifyKingdomEventStarted(long receivedFrom, NotifyKingdomEventStarted message)
        {
            GlobalMapInteraction.StartKingdomEvent();
        }

        private void OnNotifyKingdomEventSolutionSelected(long receivedFrom, NotifyKingdomEventSolutionSelected message)
        {
            var solution = Mapper.Map<NetworkKingdomEventSolution>(message.Solution);
            GlobalMapInteraction.SelectKingdomEventSolution(solution);
        }

        private void OnNotifyKingdomEventSelected(long receivedFrom, NotifyKingdomEventSelected message)
        {
            var kingdomEvent = Mapper.Map<NetworkKingdomEvent>(message.Event);
            GlobalMapInteraction.SelectKingdomEvent(kingdomEvent);
        }

        private void OnNotifyKingdomNavigationChanged(long receivedFrom, NotifyKingdomNavigationChanged message)
        {
            var navigation = Mapper.Map<KingdomNavigationType>(message.Type);
            GlobalMapInteraction.ChangeKingdomNavigation(navigation);
        }

        private async void OnNotifyCombatTurnEndSynchronizationRequired(long receivedFrom, NotifyCombatTurnEndSynchronizationRequired message)
        {
            var units = Mapper.Map<List<NetworkUnit>>(message.Units);
            var isSynced = await CombatInteraction.UpdateUnitsAsync(units, updatePosition: true);
            if (!isSynced)
            {
                // TODO: some kind of recovery?
                Logger.LogError("Failed to synchronize turn end. UnitId={UnitId}", Game.Combat.Turn.UnitId);
            }

            var confirmationMessage = new ClientCombatTurnEndSynchronized { UnitId = Game.Combat.Turn.UnitId, PlayerId = Game.LocalPlayerId };
            Send(confirmationMessage);
        }

        private void OnNotifyCombatTurnEnded(long receivedFrom, NotifyCombatTurnEnded message)
        {
            SetCombatTurnStage(NetworkCombatTurnStage.Ended);
        }

        private async void OnNotifyCombatLocalTurnEnded(long receivedFrom, NotifyCombatLocalTurnEnded message)
        {
            await WaitWhileTrue(CombatInteraction.IsRiderActive, "Waiting for all combat commands to finish before ending turn");
            CombatInteraction.EndTurnBasedCombatTurn();
        }

        private void OnNotifyCombatRecoveryRequired(long receivedFrom, NotifyCombatRecoveryRequired message)
        {
            PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.StartupDesync.Client.Key, CombatTextSeverity.Critical);

            Game.Combat.IsPrepared = false;
            Game.Combat.IsInitialized = false;
            SetCombatStage(NetworkCombatStage.Idle);
        }

        private async void OnNotifyAIActionExecuted(long receivedFrom, NotifyAIActionSelected message)
        {
            if (Game.ArmyCombat != null)
            {
                return;
            }

            await WaitWhileTrue(() => Game.Combat.Turn == null, "Waiting for turn to initialize before saving AI actions");

            var aiAction = Mapper.Map<NetworkAIAction>(message.Action);
            Game.Combat.Turn.AIActions.Add(aiAction);
        }

        private async void OnNotifyGlobalMapCommonPopupShown(long receivedFrom, NotifyGlobalMapCommonPopupShown message)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCommonPopup, message.PlayerId);

            var popup = Mapper.Map<NetworkGlobalMapCommonPopup>(message.Popup);

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
            GlobalMapInteraction.SelectLeaderLevelingSkill(message.Id);
        }

        private void OnNotifyGlobalMapCrusadeArmyLeaderLevelingConfirmed(long receivedFrom, NotifyGlobalMapCrusadeArmyLeaderLevelingConfirmed message)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling);

            GlobalMapInteraction.ConfirmLeaderLeveling();
        }

        private void OnNotifyGlobalMapCrusadeArmyLeaderLevelingClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyLeaderLevelingClosed message)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling);

            GlobalMapInteraction.CloseLeaderLeveling();
        }

        private void OnNotifyGlobalMapMagicSpellUsed(long receivedFrom, NotifyGlobalMapMagicSpellUsed message)
        {
            var spell = Mapper.Map<NetworkGlobalMapMagicSpell>(message.Spell);

            GlobalMapInteraction.UseSpell(spell);
        }

        private void OnNotifyGlobalMapCrusadeArmyRecruitCartClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyRecruitCartClosed message)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapRecruitment);
            GlobalMapInteraction.CloseRecruitments();
        }

        private void OnNotifyGlobalMapCrusadeArmyDismissed(long receivedFrom, NotifyGlobalMapCrusadeArmyDismissed message)
        {
            var army = Mapper.Map<NetworkGlobalMapArmy>(message.Army);

            GlobalMapInteraction.DismissCrusadeArmy(army);
        }

        private void OnNotifyGlobalMapRecruitmentClosed(long receivedFrom, NotifyGlobalMapRecruitmentClosed message)
        {
            // no need for specific removal as recruitment is already closed
            ResetPlayersTracker(Game.PlayersInGlobalMapRecruitment);

            GlobalMapInteraction.CloseRecruitments();
        }

        private void OnNotifyGlobalMapRecruitmentShown(long receivedFrom, NotifyGlobalMapRecruitmentShown message)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapRecruitment, message.PlayerId);
            UpdateGlobalMapRecruitmentUIState();

            GlobalMapInteraction.OpenRecruitments();
        }

        private void OnNotifyGlobalMapResourcesBought(long receivedFrom, NotifyGlobalMapResourcesBought message)
        {
            var globalMapResourceOrder = Mapper.Map<NetworkGlobalMapResourceOrder>(message.Order);

            GlobalMapInteraction.BuyResources(globalMapResourceOrder);
        }

        private void OnNotifyGlobalMapUnitsRecruited(long receivedFrom, NotifyGlobalMapUnitsRecruited message)
        {
            var globalMapUnitRecruitmentOrder = Mapper.Map<NetworkGlobalMapUnitRecruitmentOrder>(message.Order);

            GlobalMapInteraction.BuyUnits(globalMapUnitRecruitmentOrder);
        }

        private void OnNotifyGlobalMapRecruitmentPrevArmySelected(long receivedFrom, NotifyGlobalMapRecruitmentPrevArmySelected message)
        {
            GlobalMapInteraction.SelectPrevRecruitmentArmy();
        }

        private void OnNotifyGlobalMapRecruitmentNextArmySelected(long receivedFrom, NotifyGlobalMapRecruitmentNextArmySelected message)
        {
            GlobalMapInteraction.SelectNextRecruitmentArmy();
        }

        private void OnNotifyGlobalMapRecruitmentMercenariesRerolled(long receivedFrom, NotifyGlobalMapRecruitmentMercenariesRerolled message)
        {
            GlobalMapInteraction.RerollRecruitmentMercenaries();
        }

        private void OnNotifyGlobalMapCrusadeArmyBuyLeaderClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyBuyLeaderClosed globalMapCrusadeArmyBuyLeaderClosed)
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyBuyLeader, globalMapCrusadeArmyBuyLeaderClosed.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateAfterBuyLeader();

            GlobalMapInteraction.CloseBuyLeaderScreen();
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderRecruitClicked(long receivedFrom, NotifyGlobalMapCrusadeArmySetLeaderRecruitClicked globalMapCrusadeArmySetLeaderRecruitClicked)
        {
            GlobalMapInteraction.ClickRecruitmentOnSetLeaderScreen();
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderClearClicked(long receivedFrom, NotifyGlobalMapCrusadeArmySetLeaderClearClicked globalMapCrusadeArmyInfoSetLeaderCleared)
        {
            GlobalMapInteraction.ClearLeaderOnCrusdeArmyInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderClosed(long receivedFrom, NotifyGlobalMapCrusadeArmySetLeaderClosed globalMapCrusadeArmyInfoSetLeaderClosed)
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmySetLeader, globalMapCrusadeArmyInfoSetLeaderClosed.PlayerId);

            GlobalMapInteraction.CloseCrusadeArmySetLeaderInfo();

            UpdateGlobalMapCrusadeArmyInfoUIStateAfterSetLeader();
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoCartNameChanged(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoCartNameChanged message)
        {
            var army = Mapper.Map<NetworkGlobalMapArmy>(message.Army);

            GlobalMapInteraction.SetCrusadeArmyInfoCartName(army);
        }

        private void OnNotifyGlobalMapCrusadeArmyMainCartClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyMainCartClosed message)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyInfo);

            GlobalMapInteraction.CloseCrusadeArmyMainInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmyCreated(long receivedFrom, NotifyGlobalMapCrusadeArmyCreated message)
        {
            GlobalMapInteraction.CreateCrusadeArmy();

            UpdateGlobalMapRecruitmentUIState();
            UpdateGlobalMapCrusadeArmyInfoUIState();
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoShown(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoShown message)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyInfo, message.PlayerId);

            GlobalMapInteraction.OpenCrusadeArmyInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmiesMerging(long receivedFrom, NotifyGlobalMapCrusadeArmiesMerging message)
        {
            GlobalMapInteraction.OpenCrusadeArmiesMergeScreen();
        }

        private void OnNotifyGlobalMapCrusadeArmyLeaderActionExecuted(long receivedFrom, NotifyGlobalMapCrusadeArmyLeaderActionExecuted message)
        {
            var leader = Mapper.Map<NetworkGlobalMapArmyLeader>(message.Leader);
            var actionType = Mapper.Map<NetworkGlobalMapArmyLeaderActionType>(message.Type);

            GlobalMapInteraction.RunLeaderAction(leader, actionType);
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoPrevMergeArmySelected(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoPrevMergeArmySelected globalMapCrusadeArmyInfoPrevMergeArmySelected)
        {
            GlobalMapInteraction.SelectPrevCrusadeArmyInfoMergeArmy();
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoNextMergeArmySelected(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoNextMergeArmySelected globalMapCrusadeArmyInfoNextMergeArmySelected)
        {
            GlobalMapInteraction.SelectNextCrusadeArmyInfoMergeArmy();
        }

        private void OnNotifyGlobalMapCrusadeArmyMergeCartClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyMergeCartClosed globalMapCrusadeArmyInfoMergeClosed)
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyInfoMerge, globalMapCrusadeArmyInfoMergeClosed.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateAfterMerge();

            GlobalMapInteraction.CloseCrusadeArmyMergeInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmySquadsMovedToSecondArmy(long receivedFrom, NotifyGlobalMapCrusadeArmySquadsMovedToSecondArmy crusadeArmySquadsMovedToSecondArmy)
        {
            GlobalMapInteraction.MoveCrusadeArmySquadsToSecondArmy();
        }

        private void OnNotifyGlobalMapCrusadeArmySquadsMovedToMainArmy(long receivedFrom, NotifyGlobalMapCrusadeArmySquadsMovedToMainArmy crusadeArmySquadsMovedToMainArmy)
        {
            GlobalMapInteraction.MoveCrusadeArmySquadsToMainArmy();
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoClosed globalMapCrusadeArmyInfoClosed)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyInfo);
            GlobalMapInteraction.CloseCrusadeArmyInfo();
        }

        private void OnNotifyGlobalMapCrusadeArmySquadDismissed(long receivedFrom, NotifyGlobalMapCrusadeArmySquadDismissed crusadeArmySquadDismissed)
        {
            var squadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadDismissed.SquadSlot);

            GlobalMapInteraction.DismissCrusadeArmySquad(squadSlot);
        }

        private void OnNotifyGlobalMapCrusadeArmyMergedInOne(long receivedFrom, NotifyGlobalMapCrusadeArmyMergedInOne crusadeArmyMergedInOne)
        {
            var squadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmyMergedInOne.SquadSlot);

            GlobalMapInteraction.MergeInOneCrusadeArmySquad(squadSlot);
        }

        private void OnNotifyGlobalMapCrusadeArmySquadSplitRequested(long receivedFrom, NotifyGlobalMapCrusadeArmySquadSplitRequested crusadeArmySquadSplitRequested)
        {
            var sourceSquadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadSplitRequested.SourceSquadSlot);
            var targetSquadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadSplitRequested.TargetSquadSlot);

            GlobalMapInteraction.RunSplitRequestForCrusadeArmySquad(sourceSquadSlot, targetSquadSlot, crusadeArmySquadSplitRequested.Count);
        }

        private void OnNotifyGlobalMapCrusadeArmySquadsSwitched(long receivedFrom, NotifyGlobalMapCrusadeArmySquadsSwitched crusadeArmySquadsSwitched)
        {
            var sourceSquadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadsSwitched.SourceSquadSlot);
            var targetSquadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadsSwitched.TargetSquadSlot);

            GlobalMapInteraction.SwitchCrusadeArmySquads(sourceSquadSlot, targetSquadSlot);
        }

        private void OnNotifyGlobalMapCrusadeArmySquadsMerged(long receivedFrom, NotifyGlobalMapCrusadeArmySquadsMerged crusadeArmySquadsMerged)
        {
            var sourceSquadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadsMerged.SourceSquadSlot);
            var targetSquadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadsMerged.TargetSquadSlot);

            GlobalMapInteraction.MergeCrusadeArmySquads(sourceSquadSlot, targetSquadSlot, crusadeArmySquadsMerged.Count);
        }

        private void OnNotifyGlobalMapCrusadeArmySquadSplitted(long receivedFrom, NotifyGlobalMapCrusadeArmySquadSplitted crusadeArmySquadSplitted)
        {
            var squadSlot = Mapper.Map<NetworkGlobalMapArmySquadSlot>(crusadeArmySquadSplitted.SquadSlot);

            GlobalMapInteraction.SplitCrusadeArmySquad(squadSlot, crusadeArmySquadSplitted.Count);
        }

        private void OnNotifyTacticalCombatRetreated(long receivedFrom, NotifyTacticalCombatRetreated tacticalCombatRetreated)
        {
            CombatInteraction.RetreatFromTacticalCombat();
        }

        private void OnNotifyTacticalCombatTotalDefenseUsed(long receivedFrom, NotifyTacticalCombatTotalDefenseUsed tacticalCombatTotalDefenseUsed)
        {
            CombatInteraction.UseTacticalCombatTotalDefense();
        }

        private void OnNotifyTacticalCombatTurnPostponed(long receivedFrom, NotifyTacticalCombatTurnPostponed tacticalCombatTurnPostponed)
        {
            CombatInteraction.PostponeTacticalCombatTurn();
        }

        private void OnNotifyTacticalUnitMoveToCommandExecuted(long receivedFrom, NotifyTacticalUnitMoveToCommandExecuted tacticalUnitMoveToCommandExecuted)
        {
            var command = Mapper.Map<NetworkTacticalUnitMoveToCommand>(tacticalUnitMoveToCommandExecuted.Command);

            CombatInteraction.RunTacticalUnitMoveToCommand(command);
        }

        private void OnNotifyTacticalUnitUseAbilityCommandExecuted(long receivedFrom, NotifyTacticalUnitUseAbilityCommandExecuted tacticalUnitUseAbilityCommandExecuted)
        {
            var command = Mapper.Map<NetworkTacticalUnitUseAbilityCommand>(tacticalUnitUseAbilityCommandExecuted.Command);

            CombatInteraction.RunTacticalUnitUseAbilityCommand(command);
        }

        private async void OnNotifyTacticalUnitAttackCommandExecuted(long receivedFrom, NotifyTacticalUnitAttackCommandExecuted message)
        {
            var command = Mapper.Map<NetworkTacticalUnitAttackCommand>(message.Command);

            await WaitWhileTrue(() => Game.ArmyCombat == null || !string.Equals(Game.ArmyCombat.Turn.UnitId, message.Command.UnitId, StringComparison.OrdinalIgnoreCase),
                "Waiting for unit turn to start");

            CombatInteraction.RunTacticalUnitAttackCommand(command);
        }

        private void OnNotifyGlobalMapCombatResultsClosed(long receivedFrom, NotifyGlobalMapCombatResultsClosed globalMapCombatResultsClosed)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCombatResults);
            GlobalMapInteraction.CloseCombatResults();
        }

        private void OnNotifyCrusadeArmyBattleResultsClosed(long receivedFrom, NotifyCrusadeArmyBattleResultsClosed crusadeArmyBattleResultsClosed)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyBattleResults);
            GlobalMapInteraction.CloseCrusadeArmyBattleResults();
        }

        private void OnNotifyCrusadeArmyBattleResultsManualCombatStarted(long receivedFrom, NotifyCrusadeArmyBattleResultsManualCombatStarted crusadeArmyBattleResultsManualCombatStarted)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyBattleResults);

            GlobalMapInteraction.StartCrusadeArmyBattleResultsManualCombat();
        }

        private async void OnNotifyTacticalCombatInitialized(long receivedFrom, NotifyTacticalCombatInitialized tacticalCombatInitialized)
        {
            await WaitWhileTrue(() => Game.ArmyCombat == null || !CombatInteraction.IsInCrusadeTacticalCombat(), "Crusade army combat has not been started yet");

            Game.ArmyCombat.AreaSeed = tacticalCombatInitialized.AreaSeed;
            Game.ArmyCombat.Seed = tacticalCombatInitialized.Seed;
            CombatInteraction.InitializeCrusadeArmyCombat();
        }

        private void OnNotifyGlobalMapAutoCrusadeCombatChanged(long receivedFrom, NotifyGlobalMapAutoCrusadeCombatChanged globalMapAutoCrusadeCombatChanged)
        {
            GlobalMapInteraction.SetAutoCrusadeCombat(globalMapAutoCrusadeCombatChanged.IsEnabled);
        }

        private void OnNotifyGlobalMapSelectedArmyChanged(long receivedFrom, NotifyGlobalMapSelectedArmyChanged globalMapSelectedArmyChanged)
        {
            var army = Mapper.Map<NetworkGlobalMapArmy>(globalMapSelectedArmyChanged.Army);

            GlobalMapInteraction.SetSelectedArmy(army);
        }

        private void OnNotifyGlobalMapTravelerModeChanged(long receivedFrom, NotifyGlobalMapTravelerModeChanged globalMapTravelerModeChanged)
        {
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
            GlobalMapInteraction.SkipDay();
        }

        private void OnNotifyGlobalMapCommonPopupDeclined(long receivedFrom, NotifyGlobalMapCommonPopupDeclined messsage)
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCommonPopup, messsage.PlayerId);

            GlobalMapInteraction.DeclineCommonPopup();
        }


        private void OnNotifyGlobalMapLocationMessageAccepted(long playerId, NotifyGlobalMapLocationMessageAccepted message)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapLocationMessage);

            GlobalMapInteraction.AcceptLocationMessageBox();
        }

        private void OnNotifyGlobalMapLocationMessageClosed(long playerId, NotifyGlobalMapLocationMessageClosed globalMapLocationMessageClosed)
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapLocationMessage, globalMapLocationMessageClosed.PlayerId);

            GlobalMapInteraction.CloseLocationMessageBox();
        }

        private void OnNotifyPolymorphicItemCreated(long playerId, NotifyPolymorphicItemCreated polymorphicItemCreated)
        {
            var polymorphicItem = Mapper.Map<NetworkPolymorphicItem>(polymorphicItemCreated.PolymorphicItem);
            GameInteraction.CreateAndEquipPolymorphicItem(polymorphicItem, createContext: true);
        }

        private void OnNotifyNewGameSequenceTerminated(long playerId, NotifyNewGameSequenceTerminated newGameSequenceTerminated)
        {
            GameInteraction.TerminateNewGameSequence();
        }

        private void OnNotifyNewGameSequenceLevelingStarted(long playerId, NotifyNewGameSequenceLevelingStarted newGameSequenceLevelingStarted)
        {
            GameInteraction.StartNewGameSequenceLeveling();
        }

        private void OnNotifyNewGameSequencePhaseChanged(long playerId, NotifyNewGameSequencePhaseChanged newGameSequencePhaseChanged)
        {
            var phase = Mapper.Map<NetworkNewGameSequencePhase>(newGameSequencePhaseChanged.Phase);

            GameInteraction.SelectNewGameSequencePhase(phase);
        }

        private void OnNotifyNewGameDifficultyChanged(long playerId, NotifyNewGameDifficultyChanged newGameDifficultyChanged)
        {
            GameInteraction.SelectNewGameDifficulty(newGameDifficultyChanged.Difficulty);
        }

        private void OnNotifyCharacterSelectionWindowClosed(long playerId, NotifyCharacterSelectionWindowClosed characterSelectionWindowClosed)
        {
            GameInteraction.CloseCharacterSelectionWindow();
        }

        private void OnNotifyCharacterSelectionWindowAccepted(long playerId, NotifyCharacterSelectionWindowAccepted characterSelectionWindowAccepted)
        {
            GameInteraction.AcceptCharacterSelectionWindow();
        }

        private void OnNotifyCharacterSelectionToggleChanged(long playerId, NotifyCharacterSelectionToggleChanged characterSelectionToggleChanged)
        {
            GameInteraction.ToggleCharacterSelectionWindow(characterSelectionToggleChanged.UnitId);
        }

        private void OnNotifyZoneLootRemoveToggleChanged(long receivedFrom, NotifyZoneLootRemoveToggleChanged zoneLootRemoveToggleChanged)
        {
            GameInteraction.UpdateZoneLootRemoveToggle(zoneLootRemoveToggleChanged.RemoveLoot);
        }

        private void OnNotifyZoneLootLeft(long receivedFrom, NotifyZoneLootLeft zoneLootLeft)
        {
            GameInteraction.LeaveZoneLoot();
        }

        private void OnNotifyZoneLootCompleted(long receivedFrom, NotifyZoneLootCompleted zoneLootCompleted)
        {
            GameInteraction.CompleteZoneLoot();
        }

        private void OnNotifyGlobalMapEncounterRolled(long receivedFrom, NotifyGlobalMapEncounterRolled message)
        {
            var encounter = Mapper.Map<NetworkGlobalMapEncounter>(message.Encounter);

            GlobalMapInteraction.RollEncounter(encounter);
        }

        private void OnNotifyGlobalMapEncounterAvoided(long receivedFrom, NotifyGlobalMapEncounterAvoided globalMapEncounterAvoided)
        {
            GlobalMapInteraction.AvoidEncounter();

            ResetPlayersTracker(Game.PlayersInGlobalMapEncounterMessage);
        }

        private void OnNotifyGlobalMapEncounterAccepted(long receivedFrom, NotifyGlobalMapEncounterAccepted notifyGlobalMapEncounterAccepted)
        {
            GlobalMapInteraction.AcceptEncounter();

            ResetPlayersTracker(Game.PlayersInGlobalMapEncounterMessage);
        }

        private void OnNotifyGlobalMapIngredientCollectionAccepted(long receivedFrom, NotifyGlobalMapCommonPopupAccepted globalMapCommonPopupAccepted)
        {
            var popup = Mapper.Map<NetworkGlobalMapCommonPopup>(globalMapCommonPopupAccepted.Popup);
            GlobalMapInteraction.AcceptCommonPopup(popup);

            ResetPlayersTracker(Game.PlayersInGlobalMapCommonPopup);
        }

        private void OnNotifyGlobalMapTravelContinued(long receivedFrom, NotifyGlobalMapTravelContinued globalMapTravelContinued)
        {
            var traveler = Mapper.Map<NetworkGlobalMapTraveler>(globalMapTravelContinued.Traveler);
            GlobalMapInteraction.ContinueTravel(traveler);
        }

        private void OnNotifyGlobalMapTravelStopped(long receivedFrom, NotifyGlobalMapTravelStopped globalMapTravelStopped)
        {
            var traveler = Mapper.Map<NetworkGlobalMapTraveler>(globalMapTravelStopped.Traveler);
            GlobalMapInteraction.StopTravel(traveler);
        }

        private void OnNotifySkipTimeStarted(long receivedFrom, NotifySkipTimeStarted skipTimeStarted)
        {
            ResetPlayersTracker(Game.PlayersInSkipTime);
            GameInteraction.StartSkipTime();
        }

        private void OnNotifySkipTimeHoursChanged(long receivedFrom, NotifySkipTimeHoursChanged skipTimeHoursChanged)
        {
            GameInteraction.UpdateSkipTimeHours(skipTimeHoursChanged.Hours);
        }

        private void OnNotifySkipTimeClosed(long receivedFrom, NotifySkipTimeClosed skipTimeClosed)
        {
            GameInteraction.CloseSkipTimeUI();
            ResetPlayersTracker(Game.PlayersInSkipTime);
        }

        private void OnNotifyGlobalMapTravelStarted(long receivedFrom, NotifyGlobalMapTravelStarted globalMapTravelStarted)
        {
            var travel = Mapper.Map<NetworkGlobalMapTravel>(globalMapTravelStarted.Travel);

            GlobalMapInteraction.StartTravel(travel);
        }

        private void OnNotifyGlobalMapRestOpened(long receivedFrom, NotifyGlobalMapRestOpened message)
        {
            GameInteraction.InitiateRest();
        }

        private void OnNotifyGlobalMapGroupChangerOpened(long receivedFrom, NotifyGlobalMapGroupChangerOpened message)
        {
            GlobalMapInteraction.OpenGroupChanger();
        }

        private void OnNotifyRestWindowClosed(long receivedFrom, NotifyRestWindowClosed message)
        {
            GameInteraction.CloseRestWindow();
        }

        private void OnNotifyGroupChangerPartyAccepted(long playerId, NotifyGroupChangerPartyAccepted groupChangerPartyAccepted)
        {
            GameInteraction.AcceptGroupChangerParty();
            ResetPlayersTracker(Game.PlayersInGroupChanger);
        }

        private void OnNotifyGroupChangerUnitClicked(long playerId, NotifyGroupChangerUnitClicked groupChangerUnitClicked)
        {
            GameInteraction.ClickGroupChangerUnit(groupChangerUnitClicked.UnitId);
        }

        private void OnNotifyGroupChangerClosed(long playerId, NotifyGroupChangerClosed groupChangerClosed)
        {
            GameInteraction.CloseGroupChangerUI();
            ResetPlayersTracker(Game.PlayersInGroupChanger);
        }

        private void OnNotifyLobbySyncStatusChanged(long receivedFrom, NotifyLobbySyncStatusChanged lobbySyncStatusChanged)
        {
            var status = Mapper.Map<NetworkLobbySyncStatus>(lobbySyncStatusChanged.Status);
            UpdateLobbySyncStatus(lobbySyncStatusChanged.PlayerId, status);
        }

        private void OnNotifyLevelingStarted(long playerId, NotifyLevelingStarted characterLevelingStarted)
        {
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
            GameInteraction.CloseVendorWindow();
        }

        private void OnNotifyVendorDealMade(long playerId, NotifyVendorDealMade made)
        {
            GameInteraction.MakeVendorDeal();
        }

        private void OnNotifyInvalidCombatTurnStarted(long playerId, NotifyInvalidCombatTurnStarted message)
        {
            PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.Turn.ClientOrderDesync.Key, CombatTextSeverity.Debug, new UnitEntityLog(message.UnitId));
            ResetCombatTurn();
            CombatInteraction.StartTurnBasedCombatTurn(message.UnitId);
        }

        private void OnNotifyRestStarted(long playerId, NotifyRestStarted started)
        {
            GameInteraction.StartRest();
        }

        private void OnNotifyCampingUnitsRoleChanged(long playerId, NotifyCampingUnitsRoleChanged changed)
        {
            var roles = Mapper.Map<List<NetworkCampingRole>>(changed.Roles);
            GameInteraction.SetCampingRoles(roles);
        }

        private void OnNotifyCampingStateChanged(long playerId, NotifyCampingStateChanged changed)
        {
            var state = Mapper.Map<NetworkCampingState>(changed.State);
            GameInteraction.SetCampingState(state);
        }

        private void OnNotifyCampingUseHealingSpellsChanged(long playerId, NotifyCampingUseHealingSpellsChanged changed)
        {
            GameInteraction.SetCampingUseHealingSpells(changed.IsOn);
        }

        private void OnNotifySpawnCampPlace(long playerId, NotifySpawnCampPlace place)
        {
            var position = Mapper.Map<NetworkVector3>(place.Position);
            GameInteraction.SpawnCampPlace(position);
        }

        private void OnNotifyInspectionKnowledgeCheckRolled(long playerId, NotifyInspectionKnowledgeCheckRolled rolled)
        {
            var check = Mapper.Map<NetworkInspectionKnowledgeCheck>(rolled.Check);
            GameInteraction.ApplyInspectionKnowledgeCheck(check);
        }

        private void OnNotifyPerceptionCheckRolled(long playerId, NotifyPerceptionCheckRolled rolled)
        {
            var check = Mapper.Map<NetworkPerceptionCheck>(rolled.Check);
            GameInteraction.ApplyPerceptionCheck(check);
        }

        private void OnNotifyStealthPerceptionCheckRolled(long playerId, NotifyStealthPerceptionCheckRolled rolled)
        {
            var check = Mapper.Map<NetworkStealthPerceptionCheck>(rolled.Check);
            GameInteraction.ApplyStealthPerceptionCheck(check);
        }

        private async void OnNotifyCombatTurnStartSynchronizationRequired(long playerId, NotifyCombatTurnStartSynchronizationRequired message)
        {
            try
            {
                await WaitWhileTrue(() => Game.Combat?.Turn == null, "Turn has not been initialized yet");

                var combatState = Mapper.Map<NetworkCombatState>(message.CombatState);
                var areaEffects = Mapper.Map<List<NetworkAreaEffect>>(message.TriggeredAreaEffects);
                Game.Combat.TriggeredAreaEffects.AddRange(areaEffects);

                await CombatInteraction.UpdateCombatStateAsync(combatState, areaEffects, false);

                Game.Combat.Turn.Seed = message.TurnSeed;

                DiceRollStorage.Reset();
                Logger.LogInformation("Dice roll storage has been reset at after syncing turn units");

                ValueGenerator.ResetSeededGenerators(Random.IdentifierLifetime.CombatTurn);

                var confirmationMessage = new ClientCombatTurnStartSynchronized { UnitId = Game.Combat.Turn.UnitId };
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
            await SendLocalRollAsync(request.PlayerId, request);
        }

        private async void OnNotifyCombatTurnStarted(long playerId, NotifyCombatTurnStarted message)
        {
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

            var delay = GetTurnStartDelay();
            Logger.LogInformation("Starting combat turn. Delay={Delay}, UnitId={UnitId}, IsAI={IsAI}, TurnSeed={TurnSeed}", delay, Game.Combat.Turn.UnitId, Game.Combat.Turn.IsAI, Game.Combat.Turn.Seed);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }

            SetCombatTurnStage(NetworkCombatTurnStage.Playing);
            CombatInteraction.StartTurnBasedCombatTurn(Game.Combat.Turn.UnitId);
        }

        private async void OnNotifyCombatInitializationRequired(long playerId, NotifyCombatInitializationRequired message)
        {
            await WaitWhileTrue(() => Game.Combat == null, "Combat has not been started on client yet. Waiting until start");

            Game.Combat.Seed = message.CombatSeed;
            Logger.LogInformation("Combat seed has been configured. Seed={Seed}", Game.Combat.Seed);

            var combatState = Mapper.Map<NetworkCombatState>(message.State);
            var areaEffects = Mapper.Map<List<NetworkAreaEffect>>(message.TriggeredAreaEffects);
            Game.Combat.TriggeredAreaEffects.AddRange(areaEffects);

            await CombatInteraction.UpdateCombatStateAsync(combatState, areaEffects, true);

            var confirmation = new ClientCombatInitializationCompleted();
            Send(confirmation);
        }

        private void OnNotifyCombatInitializationCompleted(long playerId, NotifyCombatInitializationCompleted message)
        {
            SetCombatStage(NetworkCombatStage.Playing);
            Game.Combat.IsInitialized = true;
            Game.Combat.IsPlaying = true;
            Logger.LogInformation("Combat is ready to be continued");
        }

        private async void OnNotifyDialogStarted(long playerId, NotifyDialogStarted started)
        {
            await WaitWhileTrue(() => Game.DialogState != null && Game.DialogState.IsSelectingAnswer, "Waiting until the previous answer has been processed");

            var dialog = Mapper.Map<NetworkDialog>(started.Dialog);

            if (Game.DialogState?.Dialog == null || !string.Equals(Game.DialogState.Dialog.Id, started.Dialog.Id, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("New dialog has been initiated. PreviousDialogName={PreviousDialogName}, CurrentDialogName={CurrentDialogName}", Game.DialogState?.Dialog.Name, started.Dialog.Name);
                Game.DialogState = new NetworkDialogState(dialog);
            }

            var hasStartedDialog = await DialogInteraction.StartDialogAsync(dialog);
            if (!hasStartedDialog)
            {
                Logger.LogWarning("Client dialog is already started. Id={Id}, Name={Name}", dialog.Id, dialog.Name);
            }
        }

        private void OnNotifyDialogPopupAccepted(long playerId, NotifyDialogPopupAccepted message)
        {
            var popup = Mapper.Map<NetworkDialogPopup>(message.Popup);

            DialogInteraction.AcceptDialogPopup(popup);
        }

        private void OnNotifyDialogPopupClosed(long playerId, NotifyDialogPopupClosed message)
        {
            var popup = Mapper.Map<NetworkDialogPopup>(message.Popup);

            DialogInteraction.CloseDialogPopup(popup);
        }

        private void OnNotifyDialogCueAnswerSelected(long playerId, NotifyDialogCueAnswerSelected message)
        {
            Game.DialogState.Answer = new NetworkDialogAnswer
            {
                AnswerName = message.AnswerName,
                CueName = message.CueName,
                ManualUnitSelectionId = message.ManualUnitSelectionId,
            };

            Game.DialogState.AnswerSuggestions.Clear();
            DialogInteraction.SelectDialogAnswer(message.AnswerName, message.ManualUnitSelectionId);
        }

        private async void OnNotifyDialogCueAnswerSuggested(long playerId, NotifyDialogCueAnswerSuggested message)
        {
            await WaitWhileTrue(() => Game.DialogState == null || !string.Equals(Game.DialogState.CurrentCueName, message.CueName, StringComparison.OrdinalIgnoreCase), "Waiting for dialog to initialize before suggesting cue answer");

            var suggestions = Mapper.Map<List<NetworkDialogAnswerSuggestion>>(message.Suggestions);
            DialogInteraction.MarkSuggestedDialogAnswers(suggestions);
        }

        private void OnNotifyPartyAreaTransitioned(long playerId, NotifyPartyAreaTransitioned partyLeftArea)
        {
            var transition = Mapper.Map<NetworkAreaTransition>(partyLeftArea.Transition);
            GameInteraction.LeaveArea(transition);
        }

        private void OnNotifyGamePauseEnded(long playerId, NotifyGamePauseEnded message)
        {
            if (message.AreaSeed.HasValue)
            {
                SetAreaSeed(message.AreaSeed.Value);
            }

            if (message.Party.Count > 0)
            {
                var party = Mapper.Map<List<NetworkUnit>>(message.Party);
                CombatInteraction.UpdateUnits(party, updatePosition: false);
                Logger.LogInformation("Party units have been updated");
            }

            Game.ForcedPause = null;
            GameInteraction.SetPause(false);
        }

        private void OnNotifyGameStarted(long playerId, NotifyGameStarted started)
        {
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
            UpdateSaveInfo(notifyLobbySaveGameChanged.GameId, notifyLobbySaveGameChanged.Content);

            Game.LoadedSaveSeed = notifyLobbySaveGameChanged.Seed;

            Logger.LogInformation("Game is ready to be started. SavePath={SavePath}, LoadedSaveSeed={LoadedSaveSeed}", Game.StartUp.SavePath, Game.LoadedSaveSeed);
            var confirmationMessage = new NotifyLobbySyncStatusChanged { PlayerId = Game.LocalPlayerId, Status = NetworkLobbySyncStatus.Succeed.ToString() };
            Send(confirmationMessage);
        }

        private void OnNotifyLobbyCharactersChanged(long playerId, NotifyLobbyCharactersChanged lobbyCharactersChanged)
        {
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
            Send(message);
        }

        private void InvokeOnNetworkError(string error, SocketError? socketError = null)
        {
            OnNetworkError?.Invoke();
            PlayerNotification.ShowModalMessage(error, socketError);
        }

        private TimeSpan GetTurnStartDelay()
        {
            if (!Game.Combat.Turn.IsAI)
            {
                return TimeSpan.Zero;
            }

            var settings = SettingsService.GetSettings();
            return settings.CombatTurnDelayForAI;
        }
    }
}
