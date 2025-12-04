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
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.GlobalMap;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Leveling;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Settings;
using WOTRMultiplayer.Networking;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.Networking.Messages.Requests;

namespace WOTRMultiplayer.MP.Actors
{
    public class MultiplayerClient : MultiplayerActorBase, IMultiplayerClient
    {
        private readonly IIPEndPointParser _ipEndPointParser;
        private readonly INetworkClient _networkClient;

        public Action OnNetworkError { get; set; }

        public Action<NetworkGameConnectivity> OnConnected { get; set; }

        public Action<List<NetworkCharacterOwnership>> OnGameCharactersChanged { get; set; }
        public Action<int, int> OnCharacterOwnerChanged { get; set; }

        public Action OnDisconnected { get; set; }

        public bool IsActive => _networkClient.IsActive;

        public bool IsConnecting => _networkClient.IsConnecting;

        private NetworkGameStage Status => Game?.Stage ?? NetworkGameStage.None;

        public bool IsInLobby => IsActive && Status == NetworkGameStage.Lobby;

        protected override bool HasControlOverUI => false;

        public MultiplayerClient(
            ILogger<MultiplayerClient> logger,
            IGameInteractionService gameInteractionService,
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

        public bool ReadyChanged()
        {
            Logger.LogInformation("Toggling ready status changed");
            var player = Game.Players.First(p => p.Id == Game.LocalPlayerId);
            player.IsReady = !player.IsReady;
            var readyChanged = new PlayerReadyStatusChanged { PlayerId = player.Id, IsReady = player.IsReady };
            Send(readyChanged);
            return readyChanged.IsReady;
        }

        public void Reset()
        {
            Logger.LogInformation("Resetting");

            Game?.Reset();

            _networkClient?.Reset();
        }

        public override void OnAreaScenesLoaded()
        {
            base.OnAreaScenesLoaded();
            var message = new ClientAreaLoaded();
            Send(message);
        }

        public void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer)
        {
            Logger.LogInformation("Showing dialog Cue. DialogName={DialogName}, CueName={CueName}, HasSystemAnswer={HasSystemAnswer}", dialogName, cueName, hasSystemAnswer);
            if (hasSystemAnswer)
            {
                GameInteraction.SetDialogContinueButtonState(false);
            }

            Game.Dialog.CurrentCueName = cueName;
            Game.Dialog.Answer = null;

            var message = new CueWitnessed { CueName = cueName, DialogName = dialogName };
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

            var message = new DialogCueAnswerSuggested { DialogName = dialogName, CueName = cueName, AnswerName = answerName };
            Logger.LogInformation("Sending dialog answer suggestion. DialogName={DialogName}, CueName={CueName}, AnswerName={AnswerName}", message.DialogName, message.CueName, message.AnswerName);
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

            Logger.LogInformation("Sending dialog request to host. DialogueName={DialogueName}", dialogName);
            var message = new ClientDialogStartRequested
            {
                DialogName = dialogName,
                TargetUnitId = targetUnitId,
                InitiatorUnitId = initiatorUnitId,
                MapObjectId = mapObjectId,
                SpeakerKey = speakerKey
            };
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

                var settings = SettingsService.GetSettings();
                var message = new RandomEncounterContextRequest { Timeout = settings.RestEncounterSyncTimeout };
                var response = _networkClient.SendAndWaitFor<RandomEncounterContextResponse>(message);

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
                    EnsureForcePaused(WellKnownKeys.GameNotifications.ForcedPause.RestRandomEncounterLoading.Key);
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
                var settings = SettingsService.GetSettings();
                if (!settings.SyncAICombatActions || string.IsNullOrEmpty(networkAIAction.ActionBlueprintId))
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

                var response = _networkClient.SendAndWaitFor<AIActionResponse>(message);

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

        public bool OnRequestLevelingUI(string unitId)
        {
            if (Game.Leveling != null)
            {
                return true;
            }

            var message = new ClientCharacterLevelingRequested
            {
                UnitId = unitId
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={unitId}", nameof(ClientCharacterLevelingRequested), message.UnitId);
            Send(message);
            return false;
        }

        public bool CanTogglePause(bool isPaused)
        {
            if (Game.ForcedPause != null && isPaused)
            {
                var warningText = string.IsNullOrEmpty(Game.ForcedPause.Reason) ? WellKnownKeys.GameNotifications.ForcedPause.NoPermission.Key : Game.ForcedPause.Reason;
                GameInteraction.ShowWarningNotification(warningText);
            }

            // client has no control over manual pausing at all
            return false;
        }

        public void OnAutoPausedByTrapDetection()
        {
            var message = new ClientGameAutoPaused();
            Send(message);
            // reason is null because we need to show generic 'unpausable as a client' message
            EnsureForcePaused(reason: null, removalDelay: null);
        }

        public bool OnStopGameMode(GameModeType type)
        {
            var playerId = GetLocalPlayerId();
            var isFirstTime = UnregisterGameMode(type, playerId);
            if (isFirstTime)
            {
                var message = new ClientGameModeTypeEnded { TypeId = type.Index };
                Logger.LogInformation("Sending {MessageType}. TypeId={TypeId}", nameof(ClientGameModeTypeEnded), message.TypeId);
                Send(message);

                if (type == GameModeType.Rest && Game.ForcedPause != null)
                {
                    GameInteraction.Pause(true);
                }
            }

            return true;
        }

        public bool OnClickGroupChangerUnit(string unitId)
        {
            // client is not allowed to move characters (no restrictions, just to avoid implementing extra synchronization)
            return false;
        }

        public bool OnGlobalMapSelectLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            var canSelectLocation = GameInteraction.IsAtGlobalMapLocation(globalMapLocation);
            return canSelectLocation;
        }

        public bool OnSpawnCampPlace(NetworkVector3 position)
        {
            GameInteraction.ShowWarningNotification(WellKnownKeys.GameNotifications.Rest.NoCampingPermission.Key);
            return false;
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
            Logger.LogInformation("Sending {MessageType}. TypeId={typeId}", nameof(ClientGameModeTypeStarted), message.TypeId);
            Send(message);
            return true;
        }

        protected override DiceRollValueResponse RetrieveRoll(DiceRollValueRequest rollRequest)
        {
            return _networkClient.SendAndWaitFor<DiceRollValueResponse>(rollRequest);
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
               // this is kinda special because requester is blocking the thread (most likely game main loop) until <see cref="DiceRollValueResponse"/> is received
               .On<DiceRollValueRequest>(OnDiceRollValueRequest)

               // lobby
               .On<NotifyLobbySaveGameChanged>(OnNotifyLobbySaveGameChanged)
               .On<GameServerConnectionSucceeded>(OnGameServerConnectionSucceeded)
               .On<PlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
               .On<NotifyLobbyPlayersChanged>(OnNotifyLobbyPlayersChanged)
               .On<NotifyGameCharactersChanged>(OnNotifyGameCharactersChanged)
               .On<NotifyCharactersOwnerChanged>(OnNotifyCharactersOwnerChanged)
               .On<NotifyGameStarted>(OnNotifyGameStarted)
               .On<NotifyPlayerSaveGameSyncStatusChanged>(OnNotifyPlayerSaveGameSyncStatusChanged)

               // pausing
               .On<NotifyGamePauseStarted>(OnNotifyGamePauseStarted)
               .On<NotifyGamePauseEnded>(OnNotifyGamePauseEnded)

               // area transitioning
               .On<NotifyPartyLeaveArea>(OnNotifyPartyLeaveArea)

               // leveling
               .On<NotifyCharacterLevelingStarted>(OnNotifyCharacterLevelingStarted)

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

               // dialogs
               .On<NotifyDialogStarted>(OnNotifyDialogStarted)
               .On<NotifyDialogCueAnswerSuggested>(OnNotifyDialogCueAnswerSuggested)
               .On<NotifyDialogCueAnswerSelected>(OnNotifyDialogCueAnswerSelected)

               // vendor interaction
               .On<NotifyVendorDealMade>(OnNotifyVendorDealMade)
               .On<NotifyVendorWindowClosed>(OnNotifyVendorWindowClosed)

               // inspection
               .On<NotifyPerceptionCheckRolled>(OnNotifyPerceptionCheckRolled)
               .On<NotifyInspectionKnowledgeCheckRolled>(OnNotifyInspectionKnowledgeCheckRolled)

               // group management
               .On<NotifyGroupChangerClosed>(OnNotifyGroupChangerClosed)
               .On<NotifyGroupChangerUnitClicked>(OnNotifyGroupChangerUnitClicked)
               .On<NotifyGroupChangerPartyAccepted>(OnNotifyGroupChangerPartyAccepted)

               // skip time
               .On<NotifySkipTimeClosed>(OnNotifySkipTimeClosed)
               .On<NotifySkipTimeHoursChanged>(OnNotifySkipTimeHoursChanged)
               .On<NotifySkipTimeStarted>(OnNotifySkipTimeStarted)

               // global map
               .On<NotifyGlobalMapRestMenuOpened>(OnNotifyGlobalMapRestMenuOpened)
               .On<NotifyGlobalMapTravelStarted>(OnNotifyGlobalMapTravelStarted)
               .On<NotifyGlobalMapTravelStopped>(OnNotifyGlobalMapTravelStopped)
               .On<NotifyGlobalMapTravelContinued>(OnNotifyGlobalMapTravelContinued)
               .On<NotifyGlobalMapIngredientCollectionAccepted>(OnNotifyGlobalMapIngredientCollectionAccepted)
               .On<NotifyGlobalMapLocationEntered>(OnNotifyGlobalMapLocationEntered)
               .On<NotifyGlobalMapEncounterAccepted>(OnNotifyGlobalMapEncounterAccepted)
               .On<NotifyGlobalMapEncounterAvoided>(OnNotifyGlobalMapEncounterAvoided)
               .On<NotifyGlobalMapEncounterRolled>(OnNotifyGlobalMapEncounterRolled)
               ;
        }

        private void OnNotifyGlobalMapEncounterRolled(long playerId, NotifyGlobalMapEncounterRolled globalMapEncounterRolled)
        {
            Logger.LogInformation("Sending {MessageType}. Seed={Seed}, EncounterId={EncounterId}, Position={Position}, Avoidance={Avoidance}", nameof(NotifyGlobalMapEncounterRolled), globalMapEncounterRolled.Encounter.Seed, globalMapEncounterRolled.Encounter.BlueprintId, globalMapEncounterRolled.Encounter.Position, globalMapEncounterRolled.Encounter.AvoidanceResult);
            var encounter = Mapper.Map<NetworkGlobalMapEncounter>(globalMapEncounterRolled.Encounter);

            GameInteraction.RollGlobalMapEncounter(encounter);
        }

        private void OnNotifyGlobalMapEncounterAvoided(long playerId, NotifyGlobalMapEncounterAvoided globalMapEncounterAvoided)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapLocationEntered));
            GameInteraction.AvoidGlobalMapEncounter();

            ResetPlayersTracker(Game.PlayersInGlobalMapEncounterMessage);
        }

        private void OnNotifyGlobalMapEncounterAccepted(long playerId, NotifyGlobalMapEncounterAccepted notifyGlobalMapEncounterAccepted)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapEncounterAccepted));
            GameInteraction.AcceptGlobalMapEncounter();

            ResetPlayersTracker(Game.PlayersInGlobalMapEncounterMessage);
        }

        private void OnNotifyGlobalMapLocationEntered(long playerId, NotifyGlobalMapLocationEntered globalMapLocationEntered)
        {
            Logger.LogInformation("Received {MessageType}. LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapLocationEntered), globalMapLocationEntered.Location.Id, globalMapLocationEntered.Location.Name);

            var location = Mapper.Map<NetworkGlobalMapLocation>(globalMapLocationEntered.Location);
            GameInteraction.EnterGlobalMapLocation(location);

            ResetPlayersTracker(Game.PlayersInGlobalMapLocationMessage);
        }

        private void OnNotifyGlobalMapIngredientCollectionAccepted(long playerId, NotifyGlobalMapIngredientCollectionAccepted globalMapIngredientCollectionAccepted)
        {
            Logger.LogInformation("Received {MessageType}. LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapIngredientCollectionAccepted), globalMapIngredientCollectionAccepted.Location.Id, globalMapIngredientCollectionAccepted.Location.Name);

            var location = Mapper.Map<NetworkGlobalMapLocation>(globalMapIngredientCollectionAccepted.Location);
            GameInteraction.CollectGlobalMapIngredients(location);

            ResetPlayersTracker(Game.PlayersInGlobalMapIngredientCollection);
        }

        private void OnNotifyGlobalMapTravelContinued(long playerId, NotifyGlobalMapTravelContinued globalMapTravelContinued)
        {
            Logger.LogInformation("Received {MessageType}. PlayerEdge={PlayerEdge}", nameof(NotifyGlobalMapTravelContinued), globalMapTravelContinued.State.Player.Position?.Edge);
            var globalMapState = Mapper.Map<NetworkGlobalMapState>(globalMapTravelContinued.State);
            GameInteraction.ContinueGlobalMapTravel(globalMapState);
        }

        private void OnNotifyGlobalMapTravelStopped(long playerId, NotifyGlobalMapTravelStopped globalMapTravelStopped)
        {
            Logger.LogInformation("Received {MessageType}. PlayerEdge={PlayerEdge}", nameof(NotifyGlobalMapTravelStopped), globalMapTravelStopped.State.Player.Position?.Edge);

            var globalMapState = Mapper.Map<NetworkGlobalMapState>(globalMapTravelStopped.State);
            GameInteraction.StopGlobalMapTravel(globalMapState);
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
            Logger.LogInformation("Received {MessageType}. DestinationId={DestinationId}, DestinationName={DestinationName}", nameof(NotifyGlobalMapTravelStarted), globalMapTravelStarted.Destination.Id, globalMapTravelStarted.Destination.Name);

            var destination = Mapper.Map<NetworkGlobalMapLocation>(globalMapTravelStarted.Destination);

            GameInteraction.StartGlobalMapTravel(destination);
        }

        private void OnNotifyGlobalMapRestMenuOpened(long playerId, NotifyGlobalMapRestMenuOpened globalMapRestMenuOpened)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGlobalMapRestMenuOpened));
            GameInteraction.OpenGlobalMapRestMenu();
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

        private void OnNotifyPlayerSaveGameSyncStatusChanged(long playerId, NotifyPlayerSaveGameSyncStatusChanged playerSaveGameSyncStatus)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Status={Status}", nameof(NotifyGamePauseStarted), playerSaveGameSyncStatus.PlayerId, playerSaveGameSyncStatus.Status);

            var status = Mapper.Map<NetworkPlayerSaveGameSyncStatus>(playerSaveGameSyncStatus.Status);
            UpdatePlayerSaveGameSyncStatus(playerSaveGameSyncStatus.PlayerId, status);
        }

        private void OnNotifyGamePauseStarted(long playerId, NotifyGamePauseStarted pauseStarted)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGamePauseStarted));
            EnsureForcePaused(null);
            GameInteraction.Pause(true);
        }

        private void OnNotifyCharacterLevelingStarted(long playerId, NotifyCharacterLevelingStarted started)
        {
            Logger.LogInformation("Received {MessageType}. UnitId={unitId}", nameof(NotifyCharacterLevelingStarted), started.UnitId);
            Game.Leveling = new NetworkLeveling(started.UnitId);
            GameInteraction.StartLeveling(started.UnitId);
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
            GameInteraction.AddCombatText(WellKnownKeys.GameNotifications.Combat.ClientTurnOrderDesync.Key);
            Game.Combat.Turn = null;
            GameInteraction.StartTurnBasedCombatTurn(started.UnitId);
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
            Logger.LogInformation("Received {MessageType}. TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}, StatType={StatType}, DC={DC}", nameof(NotifyInspectionKnowledgeCheckRolled), rolled.Check.TargetUnitId, rolled.Check.InitiatorUnitId, rolled.Check.StatType, rolled.Check.DC);
            var check = Mapper.Map<NetworkInspectionKnowledgeCheck>(rolled.Check);
            GameInteraction.ApplyInspectionKnowledgeCheck(check);
        }

        private void OnNotifyPerceptionCheckRolled(long playerId, NotifyPerceptionCheckRolled rolled)
        {
            Logger.LogInformation("Received {MessageType}. UnitId={UnitId}, MapObjectId={MapObjectId}", nameof(NotifyPerceptionCheckRolled), rolled.Check.UnitId, rolled.Check.MapObject.Id);

            var check = Mapper.Map<NetworkPerceptionCheck>(rolled.Check);
            GameInteraction.ApplyPerceptionCheck(check);
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
                await GameInteraction.UpdateCombatStateAsync(combatState, false);

                var message = new ClientCombatTurnSynchronized { UnitId = unitId };
                Logger.LogInformation("Units have been synchronized. Sending {MessageType} confirmation. UnitId={UnitId}", nameof(NotifyCombatTurnSynchronizationRequired), unitId);
                Send(message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to sync units");
                throw;
            }
        }

        private async void OnDiceRollValueRequest(long playerId, DiceRollValueRequest request)
        {
            Logger.LogInformation("Received {MessageType}. RollId={RollId}", nameof(DiceRollValueRequest), request.RollId);
            // either proxied request for another player or host
            await SendLocalRollAsync(request.PlayerId ?? NetworkingConsts.HostPlayerId, request);
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
            GameInteraction.StartTurnBasedCombatTurn(Game.Combat.Turn.UnitId);
        }

        private async void OnNotifyCombatInitialized(long playerId, NotifyCombatInitialized combatInitialized)
        {
            Logger.LogInformation("Received {MessageType}. Units={Units}", nameof(NotifyCombatInitialized), combatInitialized.CombatState.Units.Count);

            if (Game.Combat == null)
            {
                Logger.LogWarning("Combat has not been started on client yet. Waiting until start");
                while (Game.Combat == null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }
            }

            var combatState = Mapper.Map<NetworkCombatState>(combatInitialized.CombatState);
            await GameInteraction.UpdateCombatStateAsync(combatState, true);

            Logger.LogInformation("Sending {MessageType}", nameof(ClientCombatInitialized));
            var message = new ClientCombatInitialized();
            Send(message);

            Game.Combat.IsInitialized = true;
        }

        private async void OnNotifyDialogStarted(long playerId, NotifyDialogStarted started)
        {
            Logger.LogInformation("Received {MessageType}. DialogueName={DialogueName}, TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}", nameof(NotifyDialogStarted), started.DialogName, started.TargetUnitId, started.InitiatorUnitId);

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
                Logger.LogInformation("New dialog has been initiated. PreviousDialogName={PreviousDialogName}, CurrentDialogName={CurrentDialogName}", Game.Dialog?.Name, started.DialogName);
                Game.Dialog = new NetworkDialog(started.DialogName);
            }

            var hasStartedDialog = await GameInteraction.StartDialogAsync(started.DialogName, started.TargetUnitId, started.InitiatorUnitId, started.MapObjectId, started.SpeakerKey);
            if (!hasStartedDialog)
            {
                Logger.LogWarning("Client dialog is already started. DialogName={DialogName}", started.DialogName);
            }
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
            GameInteraction.SelectDialogAnswer(selected.DialogName, selected.CueName, selected.AnswerName, selected.ManualUnitSelectionId);
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

            List<NetworkDialogAnswerSuggestion> suggestions = [.. suggested.Suggestions.Select(x => new NetworkDialogAnswerSuggestion { AnswerName = x.AnswerName, Players = [.. x.Players] })];
            GameInteraction.MarkSuggestedDialogAnswers(suggestions);
        }

        private void OnNotifyPartyLeaveArea(long playerId, NotifyPartyLeaveArea area)
        {
            Logger.LogInformation("Received {MessageType}. AreaExitId={AreaExitId}", nameof(NotifyPartyLeaveArea), area.AreaExitId);
            GameInteraction.LeaveArea(area.AreaExitId);
        }

        private void OnNotifyGamePauseEnded(long playerId, NotifyGamePauseEnded changed)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGamePauseEnded));
            Game.ForcedPause = null;
            GameInteraction.Pause(false);
        }

        private void OnNotifyGameStarted(long playerId, NotifyGameStarted started)
        {
            Logger.LogInformation("Received {MessageType}", nameof(NotifyGameStarted));
            if (string.IsNullOrEmpty(Game.SaveFilePath))
            {
                Logger.LogCritical("Trying to start a game with missing save file path");
                return;
            }

            LoadSaveGame();
        }

        private void OnNotifyCharactersOwnerChanged(long playerId, NotifyCharactersOwnerChanged changed)
        {
            Logger.LogInformation("Received {MessageType}. OwnersCount={OwnersCount}", nameof(NotifyCharactersOwnerChanged), changed.Owners.Count);
            try
            {
                foreach (var owner in changed.Owners)
                {
                    var player = Game.Players.FirstOrDefault(p => p.Id == owner.PlayerId);
                    if (player == null)
                    {
                        Logger.LogWarning("Unable to assign character ownership for missing player. PlayerId={PlayerId}", owner.PlayerId);
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

        private void OnNotifyLobbySaveGameChanged(long playerId, NotifyLobbySaveGameChanged notifyLobbySaveGameChanged)
        {
            Logger.LogInformation("Received {MessageType}. GameStatus={GameStatus}, Size={Size}, IsForceLoad={IsForceLoad}", nameof(NotifyLobbySaveGameChanged), Game.Stage, notifyLobbySaveGameChanged.Content.Length, notifyLobbySaveGameChanged.IsForceLoad);

            Game.SaveFilePath = StoreSaveFile(notifyLobbySaveGameChanged.Content);
            Game.Id = notifyLobbySaveGameChanged.GameId;

            // TODO: check dlc ?

            if (notifyLobbySaveGameChanged.IsForceLoad)
            {
                ForceLoadGame();
                return;
            }

            Logger.LogInformation("Game is ready to be started. SavePath={SavePath}", Game.SaveFilePath);
            var confirmationMessage = new ClientSaveGameSyncChanged { Status = NetworkPlayerSaveGameSyncStatus.Succeed.ToString() };
            Send(confirmationMessage);
        }

        private void OnPlayerReadyStatusChanged(long playerId, PlayerReadyStatusChanged readyStatusChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, IsReady={IsReady}", nameof(PlayerReadyStatusChanged), readyStatusChanged.PlayerId, readyStatusChanged.IsReady);

            UpdatePlayerReadyStatus(readyStatusChanged.PlayerId, readyStatusChanged.IsReady);
        }

        private void OnNotifyGameCharactersChanged(long playerId, NotifyGameCharactersChanged changed)
        {
            Logger.LogInformation("Received {MessageType}. Portraits={Portraits}", nameof(NotifyGameCharactersChanged), string.Join(";", changed.Characters.Select(c => c.Portrait)));

            var characters = Mapper.Map<List<NetworkCharacterOwnership>>(changed.Characters);
            Game.Characters.Clear();
            Game.Characters.AddRange(characters);
            OnGameCharactersChanged?.Invoke(Game.Characters);
        }

        private void OnNotifyLobbyPlayersChanged(long playerId, NotifyLobbyPlayersChanged changed)
        {
            Logger.LogInformation("Received {MessageType}. PlayersCount={PlayersCount}", nameof(NotifyLobbyPlayersChanged), changed.Players.Count);

            foreach (var changedPlayer in changed.Players)
            {
                var existingPlayer = Game.Players.FirstOrDefault(p => p.Id == changedPlayer.Id);
                if (existingPlayer == null)
                {
                    var newPlayer = Mapper.Map<NetworkPlayer>(changedPlayer);
                    Game.Players.Add(newPlayer);
                    continue;
                }

                CleanupPlayer(existingPlayer);
                ShowPlayerDisconnectedMessage(existingPlayer);
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
                    break;
                default:
                    socketError = socketException.SocketErrorCode;
                    error = WellKnownKeys.MultiplayerClient.Errors.NetworkError.Key;
                    break;
            }

            InvokeOnNetworkError(error, socketError);
        }

        private void InvokeOnNetworkError(string error, SocketError? socketError = null)
        {
            OnNetworkError?.Invoke();
            GameInteraction.ShowModalMessage(error, socketError);
        }

        private void OnGameServerConnectionSucceeded(long playerId, GameServerConnectionSucceeded connectionSucceeded)
        {
            Logger.LogInformation("Received {MessageType}. ClientPlayerId={ClientPlayerId}, RestBanterSeed={RestBanterSeed}", nameof(GameServerConnectionSucceeded), connectionSucceeded.ClientPlayerId, connectionSucceeded.RestBanterSeed);

            Game.LocalPlayerId = connectionSucceeded.ClientPlayerId;
            Game.RestBanterSeed = connectionSucceeded.RestBanterSeed;

            var settings = Mapper.Map<NetworkGameSettings>(connectionSucceeded.GameSettings);
            GameInteraction.ApplyGameSettings(settings);

            var message = new ClientGameServerConnectionConfirmed() { PlayerName = SettingsService.GetSettings().PlayerName };
            Logger.LogInformation("Sending {MessageType}. PlayerName={PlayerName}", nameof(ClientGameServerConnectionConfirmed), message.PlayerName);
            Send(message);
        }
    }
}
