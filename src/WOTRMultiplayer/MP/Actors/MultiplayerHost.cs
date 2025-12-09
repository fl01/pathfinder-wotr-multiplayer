using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using Kingmaker.Controllers.Rest;
using Kingmaker.GameModes;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.MP.Actors;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Content;
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
    public class MultiplayerHost : MultiplayerActorBase, IMultiplayerHost
    {
        private readonly INetworkServer _networkServer;

        private NetworkGameStage Status => Game?.Stage ?? NetworkGameStage.None;

        public Action<NetworkGameConnectivity> OnConnected { get; set; }

        public bool IsActive => _networkServer.IsActive;

        public bool IsInLobby => IsActive && Status == NetworkGameStage.Lobby;

        protected override bool HasControlOverUI => true;

        public MultiplayerHost(
            ILogger<MultiplayerHost> logger,
            IGameInteractionService gameInteractionService,
            IMultiplayerSettingsService multiplayerSettingsProvider,
            IFileSystemService fileSystemService,
            INetworkServer networkServer,
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
                  networkServer)
        {
            _networkServer = networkServer;
        }

        public void Create(string saveFilePath, string gameId, List<NetworkCharacter> characters)
        {
            if (_networkServer.IsActive)
            {
                _networkServer.Reset();
            }

            SetupNetworkMessageHandlers();

            Game?.Reset();

            Game = new NetworkGame(saveFilePath)
            {
                LocalPlayerId = NetworkingConsts.HostPlayerId,
                Id = gameId,
                SessionSeed = CreateRandomSeed()
            };

            Game.Characters.AddRange(characters);
            var settings = SettingsService.GetSettings();
            _networkServer.Start(settings.HostPortRangeStart, settings.HostPortRangeEnd, settings.NetworkAwaiterTimeout);

            Logger.LogInformation("Host has been created. SavePath={SavePath}, Portraits={Portraits}", saveFilePath, string.Join(";", Game.Characters.Select(c => c.Portrait)));
        }

        public void UpdateSaveGame(string saveFilePath, string gameId, List<NetworkCharacter> characters)
        {
            Game.Id = gameId;
            Game.SaveFilePath = saveFilePath;
            Game.Characters.Clear();
            Game.Characters.AddRange(characters);
            var host = GetHost();
            foreach (var character in characters)
            {
                character.Owner = host;
            }

            Logger.LogInformation("Notifying game characters changed. Portraits={Portraits}", string.Join(";", Game.Characters.Select(c => c.Portrait)));
            var message = CreateNotifyGameCharactersChanged();
            _networkServer.SendAll(message);
        }

        /// <summary>
        /// Save file info (Persistence.SaveInfo) has no character IDs loaded, so we are kinda forced do deal with indexes
        /// Although that info could be parsed manually (saveInfo.Reference.saver.ReadJson("player")), however, it causes a noticeable delay as it's quite a big data portion of the save file
        /// Also, any party order manipulations (e.g. reordering characters through party list) don't cause any side-effects for these indexes, so, this should work fine
        /// </summary>
        /// <param name="characterIndex"></param>
        /// <param name="playerIndex"></param>
        public void ChangeCharacterOwner(int characterIndex, int playerIndex)
        {
            lock (ActionLock)
            {
                if (Game.Players.Count <= playerIndex)
                {
                    Logger.LogError("Unable to change character owner as playerIndex is out of range. PlayersCount={PlayersCount}, PlayerIndex={PlayerIndex}", Game.Players.Count, playerIndex);
                    return;
                }

                var player = Game.Players[playerIndex];

                if (Game.Characters.Count <= characterIndex)
                {
                    Logger.LogWarning("Unable to change character owner as characterIndex is out of range. CharacterOwnersCount={CharacterOwnersCount}, CharacterIndex={CharacterIndex}", Game.Characters.Count, characterIndex);
                    return;
                }

                var character = Game.Characters[characterIndex];
                if (character.Owner == player)
                {
                    return;
                }

                character.Owner = player;
                Logger.LogInformation("New character owner. CharacterName={CharacterName}, PlayerId={PlayerId}, PlayerName={PlayerName}", character.Name, player.Id, player.Name);

                // UnitId is available once we are in the loaded game
                // any further changes should be recorded so we can automatically assign ownership on adding or removing companions
                if (!string.IsNullOrEmpty(character.UnitId))
                {
                    UpdateCharacterOwnershipHistory(character);
                }

                var charactersOwnerChanged = CreateNotifyCharactersOwnerChanged();
                _networkServer.SendAll(charactersOwnerChanged);
            }
        }

        public void Reset()
        {
            Logger.LogInformation("Resetting");
            lock (ActionLock)
            {
                Game?.Reset();
            }

            _networkServer.Reset();
        }

        public void Start()
        {
            Logger.LogInformation("Starting game...");
            // it should be fine to block current thread
            var content = FileSystem.GetRawFileContent(Game.SaveFilePath);
            if (content == null)
            {
                Logger.LogError("Unable to start a game due to missing save file. Path={Path}", Game.SaveFilePath);
                return;
            }

            var host = GetHost();
            UpdatePlayerSaveGameSyncStatus(host, NetworkPlayerSaveGameSyncStatus.Succeed);

            var saveSyncStatusChanged = new NotifyPlayerSaveGameSyncStatusChanged
            {
                Status = host.SaveGameSyncStatus.ToString(),
                PlayerId = host.Id,
            };
            _networkServer.SendAll(saveSyncStatusChanged);

            Game.Stage = NetworkGameStage.SyncingSaveGame;

            var saveGameChanged = new NotifyLobbySaveGameChanged { GameId = Game.Id, Content = content, IsForceLoad = false };
            Logger.LogInformation("Sending save game file content to all players. Size={Size}", saveGameChanged.Content.Length);
            _networkServer.SendAll(saveGameChanged);
            Logger.LogInformation("Waiting for players to confirm save game sync. GameStatus={GameStatus}", Game.Stage);

            TryStartGame();
        }

        public void OnAreaLoadingComplete()
        {
            TryEndForcedPause();
        }

        public void LeaveArea(string areaExitId)
        {
            Logger.LogInformation("Sending {MessageType}. AreaExitId={AreaExitId}", nameof(NotifyPartyLeaveArea), areaExitId);
            var message = new NotifyPartyLeaveArea { AreaExitId = areaExitId };
            _networkServer.SendAll(message);
        }

        public void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer)
        {
            Logger.LogInformation("Showing dialog Cue. DialogName={DialogName}, CueName={CueName}, HasSystemAnswer={HasSystemAnswer}", dialogName, cueName, hasSystemAnswer);
            if (hasSystemAnswer)
            {
                GameInteraction.SetDialogContinueButtonState(false);
            }

            Game.Dialog.CurrentCueName = cueName;
            AddCueWitness(cueName, Game.LocalPlayerId);

            TryEnableDialogContinueButton();
        }

        public bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId)
        {
            Logger.LogInformation("Select Dialog Answer. DialogName={DialogName}, CueName={CueName} Answer={Answer}, IsExitAnswer={IsExitAnswer}, ManualUnitSelectionId={ManualUnitSelectionId}", dialogName, cueName, answerName, isExitAnswer, manualUnitSelectionId);

            var missingPlayers = GetPlayersWhoHaveNotSeenCueYet(cueName);
            if (missingPlayers.Count > 0)
            {
                Logger.LogWarning("Some players haven't seen the dialog yet. Players={Players}", string.Join(";", missingPlayers.Select(p => p.Name)));
                return false;
            }

            GameInteraction.ResetSuggestedDialogAnswers();
            Game.Dialog.AnswerSuggestions.Clear();

            Game.Dialog.Answer = new NetworkDialogAnswer
            {
                AnswerName = answerName,
                CueName = cueName,
                ManualUnitSelectionId = manualUnitSelectionId
            };

            /// game will do it's 'dialog stat check' rolls logic a bit later on
            /// so answer couldn't be sent right away unless it's the last one
            if (isExitAnswer)
            {
                SendSelectedAnswer();
            }


            return true;
        }

        public void SendSelectedAnswer()
        {
            if (Game.Dialog == null)
            {
                Logger.LogError("Unable to send dialog answer because dialog is null");
                return;
            }

            if (Game.Dialog.Answer == null)
            {
                Logger.LogWarning("Answer is not set, most likely it's a first dialog cue or cutscene intermission. DialogName={DialogName}", Game.Dialog.Name);
                return;
            }

            Logger.LogInformation("Sending selected answer to clients. DialogName={DialogName}, CueName={CueName}, AnswerName={AnswerName}, ManualUnitSelectionId={ManualUnitSelectionId}", Game.Dialog.Name, Game.Dialog.Answer.CueName, Game.Dialog.Answer.AnswerName, Game.Dialog.Answer.ManualUnitSelectionId);

            var message = new NotifyDialogCueAnswerSelected
            {
                DialogName = Game.Dialog.Name,
                CueName = Game.Dialog.Answer.CueName,
                AnswerName = Game.Dialog.Answer.AnswerName,
                ManualUnitSelectionId = Game.Dialog.Answer.ManualUnitSelectionId
            };

            _networkServer.SendAll(message);
            Game.Dialog.Answer = null;
        }

        public bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
        {
            var message = new NotifyDialogStarted
            {
                DialogName = dialogName,
                InitiatorUnitId = initiatorUnitId,
                TargetUnitId = targetUnitId,
                MapObjectId = mapObjectId,
                SpeakerKey = speakerKey
            };
            Logger.LogInformation("Sending dialog started to all clients. DialogName={DialogName}, TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}, MapObjectId={MapObjectId}, SpeakerKey={SpeakerKey}",
                message.DialogName, message.TargetUnitId, message.InitiatorUnitId, message.MapObjectId, message.SpeakerKey);

            Send(message);

            if (!string.Equals(Game.Dialog?.Name, dialogName, StringComparison.OrdinalIgnoreCase))
            {
                Game.Dialog = new NetworkDialog(dialogName);
            }

            return true;
        }

        /// <summary>
        /// 35 - UnitCombatPrepareController
        /// </summary>
        /// <returns></returns>
        public bool CanInitializeCombat()
        {
            return true;
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

            if (Game.Combat.Round <= 1 && !Game.Combat.IsInitialized)
            {
                var combatState = GameInteraction.GetCombatState();
                var message = new NotifyCombatInitialized
                {
                    CombatState = Mapper.Map<Networking.Messages.Contracts.NetworkCombatState>(combatState),
                    Seed = Game.Combat.Seed
                };

                _networkServer.SendAll(message);
                Game.Combat.IsInitialized = true;
                Game.Combat.PlayersCombatInitialization.TryAdd(Game.LocalPlayerId, true);
                Logger.LogInformation("Sending {MessageType}. Seed={Seed}, RoundNumber={RoundNumber}, HasSurprisingRound={HasSurprisingRound}, UnitsInCombat={UnitsInCombat}", nameof(NotifyCombatInitialized), message.Seed, message.CombatState.RoundNumber, message.CombatState.HasSurpriseRound, message.CombatState.Units.Count);
            }

            var canContinue = Game.Combat.PlayersCombatInitialization.Count >= GetPlayersCount();
            return canContinue;
        }

        public override void CombatStarted()
        {
            base.CombatStarted();

            var combatSeed = CreateRandomSeed();
            Game.Combat.Seed = combatSeed;
            Logger.LogInformation("Combat seed has been configured. Seed={Seed}", Game.Combat.Seed);
        }

        public bool IsDiceRollOwner()
        {
            return IsRolledByHost() || IsRolledByLocalPlayer();
        }

        public void OnPerceptionCheck(NetworkPerceptionCheck check)
        {
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, MapObjectId={MapObjectId}, Result={Result}", nameof(NotifyPerceptionCheckRolled), check.UnitId, check.MapObject.Id);
            var message = new NotifyPerceptionCheckRolled
            {
                Check = Mapper.Map<Networking.Messages.Contracts.NetworkPerceptionCheck>(check)
            };

            Send(message);
        }

        public void OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check)
        {
            Logger.LogInformation("Sending {MessageType}. TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}, StatType={StatType}, DC={DC}",
                nameof(NotifyInspectionKnowledgeCheckRolled), check.TargetUnitId, check.InitiatorUnitId, check.StatType, check.DC);

            var message = new NotifyInspectionKnowledgeCheckRolled
            {
                Check = Mapper.Map<Networking.Messages.Contracts.NetworkInspectionKnowledgeCheck>(check)
            };

            Send(message);
        }

        public bool OnSpawnCampPlace(NetworkVector3 position)
        {
            Logger.LogInformation("Sending spawn camp event. Position={Position}", position);
            var message = new NotifySpawnCampPlace
            {
                Position = Mapper.Map<Networking.Messages.Contracts.NetworkVector3>(position)
            };
            Send(message);

            return true;
        }

        public void OnCampingUseHealingSpellsChanged(bool isOn)
        {
            var message = new NotifyCampingUseHealingSpellsChanged { IsOn = isOn };
            Logger.LogInformation("Sending {MessageType}. IsOn={IsOn}", nameof(NotifyCampingUseHealingSpellsChanged), isOn);
            Send(message);
        }

        public void OnCampingStateChanged(NetworkCampingState state)
        {
            var message = new NotifyCampingStateChanged
            {
                State = Mapper.Map<Networking.Messages.Contracts.NetworkCampingState>(state)
            };

            Logger.LogInformation("Sending {MessageType}.CookingBlueprintRecipeId={CookingBlueprintRecipeId}, PotionBlueprintRecipeId={PotionBlueprintRecipeId}, ScrollBlueprintRecipeId={ScrollBlueprintRecipeId}, IterationsCount={IterationsCount}, AutotuneIterations={AutotuneIterations}", nameof(NotifyCampingStateChanged),
                message.State.CookingBlueprintRecipeId, message.State.PotionBlueprintRecipeId, message.State.ScrollBlueprintRecipeId, message.State.IterationsCount, message.State.AutotuneIterationsStatus);

            Send(message);
        }

        public void OnCampingUnitsRoleChanged(List<NetworkCampingRole> roles)
        {
            var rolesData = string.Join(" ,", roles.Select(r => $"[RoleType={r.RoleType} PrimaryUnit={r.PrimaryUnitId} SecondaryUnit={r.SecondaryUnitId}]"));
            Logger.LogInformation("Sending {MessageType}. RolesCount={RolesCount}, RolesData={RolesData}", nameof(NotifyCampingStateChanged), roles.Count, rolesData);

            var message = new NotifyCampingUnitsRoleChanged
            {
                Roles = Mapper.Map<List<Networking.Messages.Contracts.NetworkCampingRole>>(roles),
            };
            Send(message);
        }

        public void OnStartRest()
        {
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyRestStarted));
            var message = new NotifyRestStarted();
            Send(message);
            Game.RandomEncounter = null;
            Game.PlayersFinishedRest.Clear();
        }

        public bool OnShowRestView(RestPhase phase)
        {
            Logger.LogInformation("Showing rest view. Phase={Phase}", phase);
            if (phase == RestPhase.ShowingResults)
            {
                var localPlayer = GetLocalPlayerId();
                UpdateStartRestButtonAfterResults(localPlayer);
            }

            return true;
        }

        public void OnAfterTryRollRandomEncounter()
        {
            try
            {
                var encounterContext = GameInteraction.RemoteContext?.RandomEncounter;
                if (encounterContext == null)
                {
                    Logger.LogError("Random encounter rolling is finished, but context has not been recorded");
                    return;
                }

                if (Game.RandomEncounter != null)
                {
                    Logger.LogWarning("Previous random encounter context has not been disposed correctly");
                }

                Game.RandomEncounter = encounterContext.Recording;

                Logger.LogInformation("Random encounter context has been stored. Data={Data}", Game.RandomEncounter);

                if (Game.RandomEncounter.RandomUnitSeed.HasValue)
                {
                    var settings = SettingsService.GetSettings();
                    EnsureForcePaused(WellKnownKeys.GameNotifications.ForcedPause.RestRandomEncounterLoading.Key, settings.ForcedPauseRandomEncounterTerminationDelay);
                    GameInteraction.UpdateIsInCombatStatus();
                    GameInteraction.SetPause(true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to store random encounter context");
                throw;
            }
        }

        public NetworkAIAction OnAfterAISelectedAction(NetworkAIAction action)
        {
            try
            {
                if (Game.Combat == null || !SettingsService.GetSettings().SyncAICombatActions)
                {
                    return null;
                }

                Game.Combat.AIActions.Add(action);
                Logger.LogInformation("AI action selection has been stored. UnitId={UnitId}, ActionBlueprintId={ActionBlueprintId}, TargetId={TargetId}, Index={Index}", action.UnitId, action.ActionBlueprintId, action.TargetId, Game.Combat.AIActions.Count);
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to store random encounter context");
                throw;
            }
        }

        public void OnMakeVendorDeal()
        {
            var message = new NotifyVendorDealMade();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyVendorDealMade));
            Send(message);
        }

        public void OnCloseVendorWindow()
        {
            var message = new NotifyVendorWindowClosed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyVendorWindowClosed));
            Send(message);
        }

        public bool OnRequestLevelingUI(string unitId)
        {
            if (Game.Leveling != null)
            {
                Logger.LogWarning("Previous character leveling has not been disposed correctly. UnitId={UnitId}", Game.Leveling.UnitId);
            }

            lock (ActionLock)
            {
                Game.Leveling = new NetworkLeveling(unitId);
                var message = new NotifyCharacterLevelingStarted
                {
                    UnitId = unitId
                };
                Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}", nameof(NotifyCharacterLevelingStarted), message.UnitId);
                Send(message);
            }

            return true;
        }

        public bool CanTogglePause(bool isPaused)
        {
            lock (ActionLock)
            {
                if (isPaused)
                {
                    var isUnpaused = TryEndForcedPause();
                    if (isUnpaused)
                    {
                        return true;
                    }

                    var messageKey = Game.ForcedPause.IsLifting ? WellKnownKeys.GameNotifications.ForcedPause.IsLifting.Key : Game.ForcedPause.Reason;
                    GameInteraction.ShowWarningNotification(messageKey);
                    return false;
                }

                if (Game.ForcedPause == null)
                {
                    // removalDelay doesn't matter since returning true from this method will end pause immediately
                    EnsureForcePaused(WellKnownKeys.GameNotifications.ForcedPause.NotSyncedPauseYet.Key, removalDelay: null);
                    var localPlayer = GetLocalPlayerId();
                    Game.ForcedPause.ReadyPlayers.Add(localPlayer);

                    var pauseStarted = new NotifyGamePauseStarted();
                    Logger.LogInformation("Sending {MessageType}", nameof(NotifyGamePauseStarted));
                    Send(pauseStarted);
                    return true;
                }

                return false;
            }
        }

        public bool OnStopGameMode(GameModeType type)
        {
            var playerId = GetLocalPlayerId();
            var isFirstTime = UnregisterGameMode(type, playerId);

            if (isFirstTime && type == GameModeType.Rest && Game.ForcedPause != null)
            {
                lock (ActionLock)
                {
                    Game.ForcedPause.ReadyPlayers.Add(playerId);
                    GameInteraction.SetPause(true);
                    TryEndForcedPause();
                }
            }

            return true;
        }

        public void OnAutoPausedByTrapDetection()
        {
            lock (ActionLock)
            {
                EnsureForcePaused(WellKnownKeys.GameNotifications.ForcedPause.NoTrapDetectedYet.Key, removalDelay: null);
                var playerId = GetLocalPlayerId();
                Game.ForcedPause.ReadyPlayers.Add(playerId);
            }
        }

        public void OnCloseGroupChangerPartyUI()
        {
            ResetPlayersTracker(Game.PlayersInGroupChanger);

            var message = new NotifyGroupChangerClosed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGroupChangerClosed));
            Send(message);
        }

        public bool OnClickGroupChangerUnit(string unitId)
        {
            var everyoneIsReady = false;
            lock (ActionLock)
            {
                everyoneIsReady = Game.PlayersInGroupChanger.Count >= Game.Players.Count;
            }

            if (!everyoneIsReady)
            {
                return everyoneIsReady;
            }

            var message = new NotifyGroupChangerUnitClicked
            {
                UnitId = unitId
            };

            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}", nameof(NotifyGroupChangerUnitClicked), message.UnitId);
            Send(message);

            return true;
        }

        public void OnAcceptGroupChangerParty()
        {
            var message = new NotifyGroupChangerPartyAccepted();

            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGroupChangerPartyAccepted));
            Send(message);
            ResetPlayersTracker(Game.PlayersInGroupChanger);
        }

        public void OnGlobalMapRestMenuOpened()
        {
            var message = new NotifyGlobalMapRestMenuOpened();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapRestMenuOpened));
            Send(message);
        }

        public void OnGlobalMapStartTravel(NetworkGlobalMapLocation destination)
        {
            var message = new NotifyGlobalMapTravelStarted
            {
                Destination = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapLocation>(destination)
            };
            Logger.LogInformation("Sending {MessageType}. DestinationId={DestinationId}, DestinationName={DestinationName}", nameof(NotifyGlobalMapTravelStarted), message.Destination.Id, message.Destination.Name);
            Send(message);
        }

        public void OnSkipTimeClosed()
        {
            ResetPlayersTracker(Game.PlayersInSkipTime);

            var message = new NotifySkipTimeClosed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifySkipTimeClosed));
            Send(message);
        }

        public void OnSkipTimeHoursChanged(float hours)
        {
            var message = new NotifySkipTimeHoursChanged
            {
                Hours = hours
            };
            Logger.LogInformation("Sending {MessageType}. Hours={Hours}", nameof(NotifySkipTimeHoursChanged), message.Hours);
            Send(message);
        }

        public void OnSkipTimeStarted()
        {
            var message = new NotifySkipTimeStarted();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifySkipTimeStarted));
            Send(message);
        }

        public bool OnGlobalMapSelectLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            return true;
        }

        public void OnGlobalMapContinueTravel(NetworkGlobalMapState globalMapState)
        {
            var message = new NotifyGlobalMapTravelContinued
            {
                State = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapState>(globalMapState)
            };
            Logger.LogInformation("Sending {MessageType}. PlayerEdge={PlayerEdge}", nameof(NotifyGlobalMapTravelContinued), message.State.Player.Position?.Edge);
            Send(message);
        }

        public void OnGlobalMapStopTravel(NetworkGlobalMapState globalMapState)
        {
            var message = new NotifyGlobalMapTravelStopped
            {
                State = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapState>(globalMapState)
            };
            Logger.LogInformation("Sending {MessageType}. PlayerEdge={PlayerEdge}", nameof(NotifyGlobalMapTravelStopped), message.State.Player.Position?.Edge);
            Send(message);
        }

        public void OnGlobalMapIngredientCollectionAccepted(NetworkGlobalMapLocation globalMapLocation)
        {
            var message = new NotifyGlobalMapIngredientCollectionAccepted
            {
                Location = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapLocation>(globalMapLocation)
            };
            Logger.LogInformation("Sending {MessageType}. LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapIngredientCollectionAccepted), message.Location.Id, message.Location.Name);
            Send(message);
        }

        public void OnGlobalMapEnterLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            var message = new NotifyGlobalMapLocationEntered
            {
                Location = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapLocation>(globalMapLocation)
            };
            Logger.LogInformation("Sending {MessageType}. LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapLocationEntered), message.Location.Id, message.Location.Name);
            Send(message);
        }

        public void OnGlobalMapEncounterAccepted()
        {
            var message = new NotifyGlobalMapEncounterAccepted();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapEncounterAccepted));
            Send(message);
        }

        public void OnGlobalMapEncounterAvoided()
        {
            var message = new NotifyGlobalMapEncounterAvoided();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapEncounterAvoided));
            Send(message);
        }

        public void OnGlobalMapRandomEncounterRolled(NetworkGlobalMapEncounter globalMapEncounter)
        {
            var message = new NotifyGlobalMapEncounterRolled
            {
                Encounter = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapEncounter>(globalMapEncounter)
            };
            Logger.LogInformation("Sending {MessageType}. Seed={Seed}, EncounterId={EncounterId}, Position={Position}, Avoidance={Avoidance}", nameof(NotifyGlobalMapEncounterRolled), message.Encounter.Seed, message.Encounter.BlueprintId, message.Encounter.Position, message.Encounter.AvoidanceResult);
            Send(message);
        }

        public void OnZoneLootRemoveToggleChanged(bool removeUncollectedLoot)
        {
            var message = new NotifyZoneLootRemoveToggleChanged
            {
                RemoveLoot = removeUncollectedLoot
            };
            Logger.LogInformation("Sending {MessageType}. RemoveLoot={RemoveLoot}", nameof(NotifyZoneLootRemoveToggleChanged), message.RemoveLoot);
            Send(message);
        }

        protected override bool OnStartGameModeInternal(GameModeType type)
        {
            var playerId = GetLocalPlayerId();
            RegisterGameMode(type, playerId);

            if (type == GameModeType.Rest)
            {
                UpdateStartRestButton();
            }

            return true;
        }

        protected override DiceRollValueResponse RetrieveRoll(DiceRollValueRequest rollRequest)
        {
            // the only case when host is retrieving rolls - he is not the turn owner + it's not AI turn
            var character = GetCharacterOwnership(Game.Combat.Turn.UnitId);
            if (character?.Owner == null)
            {
                Logger.LogError("Unable to retrieve roll due to missing character ownership. UnitId={UnitId}");
                return null;
            }

            if (character.Owner.IsHost)
            {
                Logger.LogError("Host is character owner, but tries to retrieve network roll");
                return null;
            }

            return _networkServer.SendAndWaitFor<DiceRollValueResponse>(character.Owner.Id, rollRequest);
        }

        protected override void Send(object message)
        {
            _networkServer.SendAll(message);
        }

        protected override void Send(long playerId, object message)
        {
            _networkServer.Send(playerId, message);
        }

        protected override void OnLocalPlayerTurnStart()
        {
            Game.Combat.Turn.RequiresTurnEntitiesSynchronization = true;

            AddPlayerReadyStatus(PlayerTurnReadinessType.Start, Game.LocalPlayerId, Game.Combat.Turn.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitSynchronization, Game.LocalPlayerId, Game.Combat.Turn.UnitId);

            TryStartTurn();
        }

        protected void TryStartTurn()
        {
            try
            {
                Logger.LogInformation("Checking if turn could be started. Round={Round}, UnitId={UnitId}", Game.Combat.Round, Game.Combat.Turn?.UnitId);

                lock (ActionLock)
                {
                    if (Game.Combat.Turn == null)
                    {
                        Logger.LogWarning("Can't start turn because it hasn't been initialized yet");
                        return;
                    }

                    if (Game.Combat.Turn.IsInProgress)
                    {
                        Logger.LogWarning("Can't start turn because previous turn is still in progress");
                        return;
                    }

                    var desyncedPlayers = Game.Combat.PlayersNextTurnInitialization.Where(k => !string.Equals(k.Key, Game.Combat.Turn.UnitId, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (desyncedPlayers.Count > 0)
                    {
                        var players = desyncedPlayers.SelectMany(x => x.Value).Distinct().ToList();
                        Logger.LogWarning("Players have started different turn. Initiating recovering. Players={Players}", players);
                        foreach (var playerId in players)
                        {
                            var player = GetPlayer(playerId);
                            GameInteraction.AddCombatText(WellKnownKeys.GameNotifications.Combat.HostTurnOrderDesync.Key, player?.Name);

                            var desyncedTurnStartMessage = new NotifyInvalidCombatTurnStarted
                            {
                                UnitId = Game.Combat.Turn.UnitId,
                            };
                            Send(playerId, desyncedTurnStartMessage);
                        }

                        foreach (var desynced in desyncedPlayers)
                        {
                            Game.Combat.PlayersNextTurnInitialization.TryRemove(desynced.Key, out _);
                        }
                    }

                    var notInitializedPlayers = GetMissingPlayers(Game.Combat.Turn.UnitId, Game.Combat.PlayersNextTurnInitialization);
                    if (notInitializedPlayers.Count > 0)
                    {
                        Logger.LogInformation("Unable to start turn due to missing players turn initialization. MissingPlayers={MissingPlayers}", string.Join(";", notInitializedPlayers.Select(p => p.Name)));
                        return;
                    }

                    if (Game.Combat.Turn.RequiresTurnEntitiesSynchronization)
                    {
                        Game.Combat.Turn.RequiresTurnEntitiesSynchronization = false;
                        var combatState = GameInteraction.GetCombatState();
                        var syncMessage = new NotifyCombatTurnSynchronizationRequired
                        {
                            CombatState = Mapper.Map<Networking.Messages.Contracts.NetworkCombatState>(combatState),
                            UnitId = Game.Combat.Turn.UnitId
                        };
                        _networkServer.SendAll(syncMessage);
                    }

                    var notSynchronizedPlayers = GetMissingPlayers(Game.Combat.Turn.UnitId, Game.Combat.PlayersNextTurnSynchronization);
                    if (notSynchronizedPlayers.Count > 0)
                    {
                        Logger.LogInformation("Unable to start turn due to missing players turn synchronization. MissingPlayers={MissingPlayers}", string.Join(";", notSynchronizedPlayers.Select(p => p.Name)));
                        return;
                    }

                    Game.Combat.PlayersNextTurnInitialization.Clear();
                    Game.Combat.PlayersNextTurnSynchronization.Clear();

                    var message = new NotifyCombatTurnStarted
                    {
                        Round = Game.Combat.Round,
                        UnitId = Game.Combat.Turn.UnitId
                    };

                    _networkServer.SendAll(message);

                    Game.Combat.Turn.IsInProgress = true;
                }
                GameInteraction.StartTurnBasedCombatTurn(Game.Combat.Turn.UnitId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while trying to start turn");
                throw;
            }
        }

        protected override void OnAfterNetworkMessageHandled(long senderPlayerId, object message)
        {
            Logger.LogInformation("Resending message. ExceptPlayerId={ExceptPlayerId}, MessageType={MessageType}", senderPlayerId, message.GetType().Name);
            _networkServer.SendAllExcept(senderPlayerId, message);
        }

        protected override void SetupNetworkMessageHandlers()
        {
            _networkServer.OnClientConnected = OnPlayerConnected;
            _networkServer.OnClientDisconnected = OnPlayerDisconnected;
            _networkServer.OnServerStarted = OnServerStarted;

            base.SetupNetworkMessageHandlers();

            _networkServer
               // requests - this is kinda special because requester is blocking the thread (most likely main game loop) until corresponded response is received
               .On<DiceRollValueRequest>(OnDiceRollValueRequest)
               .On<DiceRollValueResponse>(null) // usable as awaiter only
               .On<RandomEncounterContextRequest>(OnRandomEncounterContextRequest)
               .On<AIActionRequest>(OnAIActionRequest)

               // lobby
               .On<NotifyPlayerSaveGameSyncStatusChanged>(OnNotifyPlayerSaveGameSyncStatusChanged)
               .On<PlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
               .On<ClientGameServerConnectionConfirmed>(OnClientGameServerConnectionConfirmed)
               // quick load by another player
               .On<NotifyLobbySaveGameChanged>(OnNotifyLobbySaveGameChanged)

               // area transitioning
               .On<ClientAreaLoaded>(OnClientAreaLoaded)

               // game modes
               .On<ClientGameModeTypeStarted>(OnClientGameModeTypeStarted)
               .On<ClientGameModeTypeEnded>(OnClientGameModeTypeEnded)

               // leveling
               .On<ClientCharacterLevelingRequested>(OnClientCharacterLevelingRequested)

               // rest
               .On<ClientRestEnded>(OnClientRestEnded)

               // combat
               .On<ClientCombatInitialized>(OnClientCombatInitialized)
               .On<ClientCombatTurnStarted>(OnClientCombatTurnStarted)
               .On<ClientCombatTurnSynchronized>(OnClientCombatTurnSynchronized)

               // dialogs
               .On<DialogCueAnswerSuggested>(OnDialogCueAnswerSuggested)
               .On<ClientDialogStartRequested>(OnClientDialogStartRequested)
               .On<CueWitnessed>(OnCueWitnessed)

               // pause
               .On<ClientGameAutoPaused>(OnClientGameAutoPaused)
               ;
        }

        private void OnClientGameAutoPaused(long playerId, ClientGameAutoPaused clientGameAutoPaused)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}", nameof(ClientGameAutoPaused), playerId);
            lock (ActionLock)
            {
                // single autopause case doesn't require clientGameAutoPaused.Reason for now
                EnsureForcePaused(WellKnownKeys.GameNotifications.ForcedPause.NoTrapDetectedYet.Key, removalDelay: null);
                Game.ForcedPause.ReadyPlayers.Add(playerId);
            }
        }

        private void OnClientCharacterLevelingRequested(long playerId, ClientCharacterLevelingRequested requested)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}", nameof(ClientCharacterLevelingRequested), playerId, requested.UnitId);

            lock (ActionLock)
            {
                if (Game.Leveling != null)
                {
                    Logger.LogInformation("Leveling is already in progress. UnitId={UnitId}, RequestedUnitId={RequestedUnitId}", Game.Leveling.UnitId, requested.UnitId);
                    var message = new NotifyCharacterLevelingStarted
                    {
                        UnitId = Game.Leveling.UnitId
                    };
                    Send(message);
                    return;
                }

                GameInteraction.StartLeveling(requested.UnitId);
            }
        }

        private async void OnAIActionRequest(long playerId, AIActionRequest request)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, ActionIndex={ActionIndex}", nameof(AIActionRequest), playerId, request.UnitId, request.ActionIndex);
            var timeout = Task.Delay(request.Timeout);
            try
            {
                NetworkAIAction networkAIAction = null;
                do
                {
                    var turnActions = Game.Combat?.AIActions;
                    if (turnActions == null || turnActions.Count == 0 || turnActions.Count <= request.ActionIndex)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(10));
                        continue;
                    }

                    networkAIAction = turnActions[request.ActionIndex];
                    break;
                }
                while (!timeout.IsCompleted);

                var message = new AIActionResponse
                {
                    UnitId = request.UnitId,
                    Action = Mapper.Map<Networking.Messages.Contracts.NetworkAIAction>(networkAIAction)
                };

                Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, ActionBlueprintId={ActionBlueprintId}, TargetId={TargetId}", nameof(AIActionResponse), playerId, message.Action?.ActionBlueprintId, message.Action?.TargetId);
                Send(playerId, message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while looking for a correct AI action. PlayerId={PlayerId}, UnitId={UnitId}", playerId, request.UnitId);
                throw;
            }
        }


        private async void OnRandomEncounterContextRequest(long playerId, RandomEncounterContextRequest request)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(RandomEncounterContextRequest));

            var timeout = Task.Delay(request.Timeout);
            while (!timeout.IsCompleted && Game.RandomEncounter == null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }

            var response = new RandomEncounterContextResponse
            {
                Encounter = Mapper.Map<Networking.Messages.Contracts.NetworkRandomEncounter>(Game.RandomEncounter)
            };

            Logger.LogInformation("Sending {MessageType}. IsAvailable={IsAvailable}", response.Encounter != null);

            Send(playerId, response);
        }

        private void OnClientRestEnded(long playerId, ClientRestEnded ended)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(ClientRestEnded), playerId);

            UpdateStartRestButtonAfterResults(playerId);
        }

        private void OnClientGameModeTypeEnded(long playerId, ClientGameModeTypeEnded ended)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, TypeId={TypeId}", nameof(ClientGameModeTypeEnded), playerId, ended.TypeId);
            var gameMode = GameModeType.All.FirstOrDefault(g => g.Index == ended.TypeId);
            UnregisterGameMode(gameMode, playerId);
            if (gameMode == GameModeType.Rest)
            {
                UpdateStartRestButton();

                if (Game.ForcedPause != null)
                {
                    lock (ActionLock)
                    {
                        Game.ForcedPause.ReadyPlayers.Add(playerId);
                        TryEndForcedPause();
                    }
                }
            }
        }

        private void OnClientGameModeTypeStarted(long playerId, ClientGameModeTypeStarted started)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, TypeId={TypeId}", nameof(ClientGameModeTypeStarted), playerId, started.TypeId);
            var gameMode = GameModeType.All.FirstOrDefault(g => g.Index == started.TypeId);
            RegisterGameMode(gameMode, playerId);
            if (gameMode == GameModeType.Rest)
            {
                UpdateStartRestButton();
            }
            else if (gameMode == GameModeType.Pause && Game.ForcedPause != null)
            {
                lock (ActionLock)
                {
                    Game.ForcedPause.ReadyPlayers.Add(playerId);
                }
            }
        }

        private void OnClientCombatTurnSynchronized(long playerId, ClientCombatTurnSynchronized synchronized)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}", nameof(ClientCombatTurnSynchronized), playerId, synchronized.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitSynchronization, playerId, synchronized.UnitId);
            TryStartTurn();
        }

        private void OnClientCombatTurnStarted(long playerId, ClientCombatTurnStarted started)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Round={Round}, UnitId={UnitId}", nameof(ClientCombatTurnStarted), playerId, started.Round, started.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.Start, playerId, started.UnitId);

            TryStartTurn();
        }

        private void OnNotifyLobbySaveGameChanged(long playerId, NotifyLobbySaveGameChanged notifyLobbySaveGameChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, IsForceLoad={IsForceLoad}, SaveGameSize={SaveGameSize}", nameof(NotifyLobbySaveGameChanged), playerId, notifyLobbySaveGameChanged.IsForceLoad, notifyLobbySaveGameChanged.Content.Length);

            OnAfterNetworkMessageHandled(playerId, notifyLobbySaveGameChanged);

            Game.SaveFilePath = StoreSaveFile(notifyLobbySaveGameChanged.Content);

            if (!notifyLobbySaveGameChanged.IsForceLoad)
            {
                Logger.LogWarning("Host received save game changed without force load flag");
            }

            ForceLoadGame();
        }

        private void OnClientCombatInitialized(long playerId, ClientCombatInitialized initialized)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(ClientCombatInitialized), playerId);
            if (Game.Combat == null)
            {
                Logger.LogWarning("Received client initialization, but combat is null. PlayerId={PlayerId}", playerId);
                return;
            }

            if (!Game.Combat.PlayersCombatInitialization.TryAdd(playerId, true))
            {
                Logger.LogWarning("Received duplicate client initialization. PlayerId={PlayerId}", playerId);
            }
        }

        private async void OnClientDialogStartRequested(long playerId, ClientDialogStartRequested requested)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, DialogName={DialogName}, TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}, MapObjectId={MapObjectId}, SpeakerKey={SpeakerKey}",
                nameof(ClientDialogStartRequested), playerId, requested.DialogName, requested.TargetUnitId, requested.InitiatorUnitId, requested.MapObjectId, requested.SpeakerKey);

            var hasBeenStarted = await GameInteraction.StartDialogAsync(requested.DialogName, requested.TargetUnitId, requested.InitiatorUnitId, requested.MapObjectId, requested.SpeakerKey);
            if (hasBeenStarted)
            {
                return;
            }

            Logger.LogInformation("Host dialog is already in progress. Sending dialog confirmation");
            var message = new NotifyDialogStarted
            {
                DialogName = requested.DialogName,
                InitiatorUnitId = requested.InitiatorUnitId,
                TargetUnitId = requested.TargetUnitId,
                MapObjectId = requested.MapObjectId,
                SpeakerKey = requested.SpeakerKey
            };

            _networkServer.SendAll(message);
        }

        private void OnDialogCueAnswerSuggested(long playerId, DialogCueAnswerSuggested suggested)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, DialogName={DialogName}, CueName={CueName}, AnswerName={AnswerName}", nameof(DialogCueAnswerSuggested), playerId, suggested.DialogName, suggested.CueName, suggested.AnswerName);

            if (Game.Dialog == null)
            {
                Logger.LogError("Received dialog answer suggestion, but there is no active dialog right now. SuggestedDialogName={SuggestedDialogName}, SuggestedCueName={SuggestedCueName}, SuggestedAnswer={SuggestedAnswer}", suggested.DialogName, suggested.CueName, suggested.AnswerName);
                return;
            }

            if (!string.Equals(Game.Dialog.Name, suggested.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog suggestion has mismatched dialog name. SuggestedDialogName={SuggestedDialogName}, CurrentDialogName={CurrentDialogName}", suggested.DialogName, Game.Dialog.Name);
                return;
            }

            if (!string.Equals(Game.Dialog.CurrentCueName, suggested.CueName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog suggestion has mismatched cue name. SuggestedCueName={SuggestedCueName}, CurrentCueName={CurrentCueName}", suggested.CueName, Game.Dialog.CurrentCueName);
                return;
            }

            Game.Dialog.AnswerSuggestions.AddOrUpdate(playerId, suggested.AnswerName, (key, existing) =>
            {
                return suggested.AnswerName;
            });

            List<NetworkDialogAnswerSuggestion> suggestions = [.. Game.Dialog.AnswerSuggestions.GroupBy(x => x.Value, x => x.Key).Select(x => new NetworkDialogAnswerSuggestion { AnswerName = x.Key, Players = [.. x] })];
            GameInteraction.MarkSuggestedDialogAnswers(suggestions);

            var notifyMessage = new NotifyDialogCueAnswerSuggested
            {
                DialogName = suggested.DialogName,
                CueName = suggested.CueName,
                Suggestions = Mapper.Map<List<Networking.Messages.Contracts.NetworkDialogAnswerSuggestion>>(suggestions),
            };
            _networkServer.SendAll(notifyMessage);
        }

        private void OnCueWitnessed(long playerId, CueWitnessed witnessed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, DialogName={DialogName}, CueName={CueName}", nameof(CueWitnessed), playerId, witnessed.DialogName, witnessed.CueName);
            if (Game.Dialog == null)
            {
                Logger.LogError("Received cue witness, but there is no active dialog right now. WitnessedDialogName={WitnessedDialogName}, WitnessedCueName={WitnessedCueName}", witnessed.DialogName, witnessed.CueName);
                return;
            }

            if (!string.Equals(Game.Dialog.Name, witnessed.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Cue witness has mismatched dialog. WitnessedDialogName={WitnessedDialogName}, CurrentDialogName={CurrentDialogName}", witnessed.DialogName, Game.Dialog.Name);
                return;
            }

            AddCueWitness(witnessed.CueName, playerId);
            TryEnableDialogContinueButton();
        }

        private async void OnDiceRollValueRequest(long playerId, DiceRollValueRequest request)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, RollId={RollId}, UnitId={UnitId}", nameof(DiceRollValueRequest), playerId, request.RollId, request.UnitId);

            var character = GetCharacterOwnership(request.UnitId);
            var isAI = GameInteraction.IsUnitAI(request.UnitId);
            // so basically in combat we need to ask another player for rolls in case he is the owner of the turn
            if (Game.Combat != null
                && !isAI
                && character?.Owner != null
                && !character.Owner.IsHost
                && character.Owner.Id != playerId)
            {
                Logger.LogInformation("Asking another client for a roll. PlayerId={PlayerId}, RollId={RollId}, UnitId={UnitId}", character.Owner.Id, request.RollId, request.UnitId);
                var message = new DiceRollValueRequest
                {
                    RollId = request.RollId,
                    UnitId = request.UnitId,
                    Timeout = request.Timeout,
                    PlayerId = playerId
                };

                var rollFromAnotherClient = _networkServer.SendAndWaitFor<DiceRollValueResponse>(playerId, message);
                Send(playerId, rollFromAnotherClient);
                return;
            }

            await SendLocalRollAsync(playerId, request);
        }

        private void OnNotifyPlayerSaveGameSyncStatusChanged(long playerId, NotifyPlayerSaveGameSyncStatusChanged playerSaveGameSyncStatusChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Status={Status}", nameof(NotifyPlayerSaveGameSyncStatusChanged), playerId, playerSaveGameSyncStatusChanged.Status);

            var status = Mapper.Map<NetworkPlayerSaveGameSyncStatus>(playerSaveGameSyncStatusChanged.Status);
            UpdatePlayerSaveGameSyncStatus(playerId, status);

            OnAfterNetworkMessageHandled(playerId, playerSaveGameSyncStatusChanged);

            TryStartGame();
        }

        private void OnPlayerReadyStatusChanged(long playerId, PlayerReadyStatusChanged readyStatusChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, IsReady={IsReady}", nameof(PlayerReadyStatusChanged), playerId, readyStatusChanged.IsReady);
            UpdatePlayerReadyStatus(playerId, readyStatusChanged.IsReady);
            // including original client so his UI can be properly updated as well
            _networkServer.SendAll(readyStatusChanged);
        }

        private void OnClientGameServerConnectionConfirmed(long playerId, ClientGameServerConnectionConfirmed connectionConfirmed)
        {
            try
            {
                Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Name={Name}", nameof(ClientGameServerConnectionConfirmed), playerId, connectionConfirmed.PlayerName);
                lock (ActionLock)
                {
                    var existingPlayer = GetPlayer(playerId);
                    if (existingPlayer == null)
                    {
                        Logger.LogError("Can't process player name update because player doesn't exist. PlayerId={playPlayerIderId}, Name={Name}", playerId, connectionConfirmed.PlayerName);
                        return;
                    }

                    if (string.IsNullOrEmpty(connectionConfirmed.PlayerName))
                    {
                        Logger.LogError("Can't process player name update because player name is missing. PlayerId={PlayerId}, Name={Name}", playerId, connectionConfirmed.PlayerName);
                        return;
                    }

                    existingPlayer.Name = connectionConfirmed.PlayerName;

                    existingPlayer.ContentState = Mapper.Map<NetworkContentState>(connectionConfirmed.ContentState);
                    var host = GetHost();
                    existingPlayer.ContentState.DiscrepantDLCs = CompareDLCs(host.ContentState, existingPlayer.ContentState);
                    existingPlayer.ContentState.DiscrepantMods = CompareMods(host.ContentState, existingPlayer.ContentState);
                    Logger.LogInformation("Player content has been checked. PlayerId={PlayerId}, DiscrepantDLCs={DiscrepantDLCs}, DiscrepantMods={DiscrepantMods}", existingPlayer.Id, existingPlayer.ContentState.DiscrepantDLCs.Count, existingPlayer.ContentState.DiscrepantMods.Count);

                    var playersChanged = CreateNotifyLobbyPlayersChanged();
                    Logger.LogInformation("Sending {MessageType} to ALL players", nameof(NotifyLobbyPlayersChanged));
                    _networkServer.SendAll(playersChanged);

                    var notifyGameCharactersChanged = CreateNotifyGameCharactersChanged();
                    Logger.LogInformation("Sending {MessageType} to new player. PlayerId={PlayerId}", nameof(NotifyCharactersChanged), playerId);
                    _networkServer.Send(playerId, notifyGameCharactersChanged);

                    var charactersOwnerChanged = CreateNotifyCharactersOwnerChanged();
                    Logger.LogInformation("Sending {MessageType} to new player. PlayerId={PlayerId}", nameof(NotifyCharactersOwnerChanged), playerId);
                    _networkServer.Send(playerId, charactersOwnerChanged);

                    OnPlayersChanged?.Invoke(Game.Players);

                    ShowPlayerConnectedMessage(existingPlayer);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to handle player name response");
                throw;
            }
        }

        private List<NetworkDiscrepantDLC> CompareDLCs(NetworkContentState hostState, NetworkContentState clientState)
        {
            var discrepantDLCs = new List<NetworkDiscrepantDLC>();
            var clientDlcs = clientState.DLCs.ToList();
            foreach (var hostDlc in hostState.DLCs)
            {
                var clientDlc = clientDlcs.FirstOrDefault(d => string.Equals(d.Id, hostDlc.Id, StringComparison.OrdinalIgnoreCase));
                NetworkDiscrepancyReason? reason = null;
                if (clientDlc == null)
                {
                    reason = NetworkDiscrepancyReason.Missing;
                }
                else if (hostDlc.IsAvailable && !clientDlc.IsAvailable)
                {
                    reason = NetworkDiscrepancyReason.Disabled;
                }
                else if (!hostDlc.IsAvailable && clientDlc.IsAvailable)
                {
                    reason = NetworkDiscrepancyReason.Extra;
                }

                if (reason != null)
                {
                    discrepantDLCs.Add(new NetworkDiscrepantDLC(hostDlc, reason.Value));
                }

                if (clientDlc != null)
                {
                    clientDlcs.Remove(clientDlc);
                }
            }

            var availableLeftovers = clientDlcs.Where(x => x.IsAvailable).Select(x => new NetworkDiscrepantDLC(x, NetworkDiscrepancyReason.Extra));
            discrepantDLCs.AddRange(availableLeftovers);

            return discrepantDLCs;
        }

        private List<NetworkDiscrepantMod> CompareMods(NetworkContentState hostState, NetworkContentState clientState)
        {
            var discrepantMods = new List<NetworkDiscrepantMod>();
            var clientMods = clientState.Mods.ToList();
            foreach (var hostMod in hostState.Mods)
            {
                var clientMod = clientMods.FirstOrDefault(m => string.Equals(m.Id, hostMod.Id, StringComparison.OrdinalIgnoreCase));
                NetworkDiscrepancyReason? reason = null;
                if (clientMod == null)
                {
                    if (hostMod.IsEnabled)
                    {
                        reason = NetworkDiscrepancyReason.Missing;
                    }
                }
                else if (hostMod.IsEnabled && !clientMod.IsEnabled)
                {
                    reason = NetworkDiscrepancyReason.Disabled;
                }
                else if (!hostMod.IsEnabled && clientMod.IsEnabled)
                {
                    reason = NetworkDiscrepancyReason.Extra;
                }
                else if (hostMod.IsEnabled && clientMod.IsEnabled && !string.Equals(clientMod.Version, hostMod.Version, StringComparison.OrdinalIgnoreCase))
                {
                    reason = NetworkDiscrepancyReason.VersionMismatch;
                }

                if (reason != null)
                {
                    discrepantMods.Add(new NetworkDiscrepantMod(hostMod, reason.Value));
                }

                if (clientMod != null)
                {
                    clientMods.Remove(clientMod);
                }
            }

            var enabledLeftovers = clientMods.Where(x => x.IsEnabled).Select(x => new NetworkDiscrepantMod(x, NetworkDiscrepancyReason.Extra)).ToList();
            discrepantMods.AddRange(enabledLeftovers);

            return discrepantMods;
        }

        private void OnPlayerConnected(long playerId)
        {
            lock (ActionLock)
            {
                var existingPlayer = GetPlayer(playerId);
                if (existingPlayer != null)
                {
                    Logger.LogWarning("Player already exists. PlayerId={PlayerId}", playerId);
                    return;
                }

                var player = new NetworkPlayer(playerId);
                Game.Players.Add(player);

                var settings = GameInteraction.GetGameSettings();
                settings.Multiplayer = SettingsService.GetSettings();

                var message = new GameServerConnectionSucceeded
                {
                    ClientPlayerId = playerId,
                    GameSettings = Mapper.Map<Networking.Messages.Contracts.NetworkGameSettings>(settings),
                    SessionSeed = Game.SessionSeed
                };
                Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, Settings={Settings}", nameof(GameServerConnectionSucceeded), message.ClientPlayerId, message.GameSettings);

                _networkServer.Send(playerId, message);
            }
        }

        private void OnPlayerDisconnected(long playerId)
        {
            lock (ActionLock)
            {
                var removedPlayer = CleanupPlayer(playerId);
                if (removedPlayer == null)
                {
                    return;
                }

                OnPlayersChanged?.Invoke(Game.Players);
                var playersChanged = CreateNotifyLobbyPlayersChanged();
                Logger.LogInformation("Sending {MessageType}", nameof(NotifyLobbyPlayersChanged));
                _networkServer.SendAllExcept(playerId, playersChanged);
                ShowPlayerDisconnectedMessage(removedPlayer);

                UpdateStartRestButton();
                UpdateStartRestButtonAfterResults(playerId);
                TryEnableDialogContinueButton();

                RemovePlayerFromTracker(Game.PlayersInSkipTime, removedPlayer.Id);
                UpdateSkipTimeUIState();

                RemovePlayerFromTracker(Game.PlayersInGroupChanger, removedPlayer.Id);
                UpdateGroupManagerUIState();

                RemovePlayerFromTracker(Game.PlayersInZoneLoot, removedPlayer.Id);
                UpdateZoneLootUIState();

                TryEndForcedPause();
            }
        }

        private NotifyLobbyPlayersChanged CreateNotifyLobbyPlayersChanged()
        {
            var playersChanged = new NotifyLobbyPlayersChanged
            {
                Players = Mapper.Map<List<Networking.Messages.Contracts.NetworkPlayer>>(GetPlayers())
            };
            return playersChanged;
        }

        private void OnServerStarted(EndPoint endpoint)
        {
            var hostPlayer = new NetworkPlayer(NetworkingConsts.HostPlayerId)
            {
                Name = SettingsService.GetSettings().PlayerName,
                ContentState = GameInteraction.GetInstalledContent(),
                IsHost = true
            };

            // no need to lock yet
            Game.Players.Add(hostPlayer);

            Game.Connectivity = new NetworkGameConnectivity
            {
                Endpoint = endpoint
            };

            foreach (var character in Game.Characters)
            {
                character.Owner = hostPlayer;
            }

            var enforcedSettings = GetEnforcedGameSettings();
            GameInteraction.ApplyGameSettings(enforcedSettings);

            OnConnected?.Invoke(Game.Connectivity);
            OnPlayersChanged?.Invoke(GetPlayers());
        }

        private NetworkGameSettings GetEnforcedGameSettings()
        {
            var settings = new NetworkGameSettings
            {
                TurnBased = new NetworkTurnBasedSettngs
                {
                    IsTurnBasedModeEnabled = true,
                    AutoStopAfterFirstMoveAction = false,
                    AutoEndTurn = false,
                },
                Main = new NetworkGameMainSettings
                {
                    LootInCombat = false,
                    QuickMovement = true
                },
                Autopause = new NetworkAutopauseSettings
                {
                    PauseOnTrapDetected = true,
                    PauseOnSpellcastInterrupted = Kingmaker.Settings.EntitiesType.None,
                    PauseOnSpellcastStarted = Kingmaker.Settings.EntitiesType.None,
                    // everything else is false for autopause
                },
            };

            return settings;
        }

        private NotifyCharactersChanged CreateNotifyGameCharactersChanged()
        {
            var message = new NotifyCharactersChanged
            {
                Characters = Mapper.Map<List<Networking.Messages.Contracts.NetworkCharacter>>(Game.Characters)
            };
            return message;
        }

        private void AddCueWitness(string cueName, long playerId)
        {
            if (Game.Dialog == null)
            {
                Logger.LogError("Trying to add witness to null dialog. CueName={CueName}, PlayerId={PlayerId}", cueName, playerId);
                return;
            }

            Game.Dialog.CueViews.AddOrUpdate(cueName, (key) => new HashSet<long>([playerId]), (key, existing) =>
            {
                existing.Add(playerId);
                return existing;
            });

            Logger.LogInformation("Cue witness has been added. CueName={CueName}, PlayerId={PlayerId}", cueName, playerId);
        }

        private List<NetworkPlayer> GetPlayersWhoHaveNotSeenCueYet(string cueName)
        {
            if (Game.Dialog == null)
            {
                Logger.LogWarning("Trying to get cue players, but dialog is null. CueName={CueName}", cueName);
                return [];
            }

            if (!Game.Dialog.CueViews.TryGetValue(cueName, out var cueViews))
            {
                Logger.LogWarning("Specified cue doesn't exist in the views history. CueName={CueName}", cueName);
                return [];
            }

            lock (ActionLock)
            {
                var players = Game.Players.Where(p => !cueViews.Contains(p.Id)).ToList();
                return players;
            }
        }

        private void TryEnableDialogContinueButton()
        {
            if (Game.Dialog == null)
            {
                Logger.LogWarning("Unable to enable continue button because current dialog is null");
                return;
            }

            var currentCue = Game.Dialog.CurrentCueName;
            if (string.IsNullOrEmpty(currentCue))
            {
                Logger.LogWarning("Current CueName is not set for the dialog");
                return;
            }

            var missingPlayers = GetPlayersWhoHaveNotSeenCueYet(currentCue);
            if (missingPlayers.Count > 0)
            {
                Logger.LogInformation("Cannot proceed with dialog yet. CurrentCue={CurrentCue}, MissingPlayers={MissingPlayers}", currentCue, string.Join(";", missingPlayers.Select(x => x.Name)));
                return;
            }

            Logger.LogInformation("All players have witnessed current cue. CueName={CueName}", currentCue);
            GameInteraction.SetDialogContinueButtonState(true);
        }

        private bool TryEndForcedPause()
        {
            try
            {
                Logger.LogInformation("Checking if forced pause could be removed. PauseIsNull={PauseIsNull}, IsLifting={IsLifting}", Game.ForcedPause == null, Game.ForcedPause?.IsLifting);

                if (Game.ForcedPause == null || Game.ForcedPause.IsLifting)
                {
                    return false;
                }

                lock (ActionLock)
                {
                    var allReady = Game.ForcedPause.ReadyPlayers.Count >= Game.Players.Count;
                    if (!allReady)
                    {
                        Logger.LogInformation("Not everyone is ready, forced pause will remain. ReadyPlayers={ReadyPlayers}", Game.ForcedPause.ReadyPlayers);
                        return false;
                    }

                    var removalDelay = Game.ForcedPause.RemovalDelay;
                    var delay = removalDelay.HasValue ? Task.Delay(removalDelay.Value) : Task.CompletedTask;
                    Game.ForcedPause.IsLifting = true;
                    Logger.LogInformation("Forced pause will be lifted soon. Delay={Delay}", removalDelay.GetValueOrDefault());
                    delay.ContinueWith(x =>
                    {
                        Game.ForcedPause = null;
                        GameInteraction.SetPause(false);
                        var message = new NotifyGamePauseEnded();
                        _networkServer.SendAll(message);
                        Logger.LogInformation("Forced pause has been lifted");
                    });
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to end forced pause");
                throw;
            }
        }

        private void OnClientAreaLoaded(long playerId, ClientAreaLoaded loaded)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(ClientAreaLoaded), playerId);
            lock (ActionLock)
            {
                EnsureForcePaused(WellKnownKeys.GameNotifications.ForcedPause.AreaLoading.Key);
                Game.ForcedPause.ReadyPlayers.Add(playerId);
            }

            TryEndForcedPause();
        }

        private void TryStartGame()
        {
            var canStart = false;

            lock (ActionLock)
            {
                canStart = Game.Players.All(p => p.SaveGameSyncStatus == NetworkPlayerSaveGameSyncStatus.Succeed);
            }

            if (canStart)
            {
                Logger.LogInformation("Starting game");
                _networkServer.SendAll(new NotifyGameStarted());
                LoadSaveGame();
            }
        }

        private NotifyCharactersOwnerChanged CreateNotifyCharactersOwnerChanged()
        {
            var charactersOwnerChanged = new NotifyCharactersOwnerChanged
            {
                Owners = [.. Game.Characters.Select((character, index) => new Networking.Messages.Contracts.NetworkCharacterOwner { CharacterIndex = index, PlayerId = character.Owner.Id })]
            };

            return charactersOwnerChanged;
        }

        private void UpdateStartRestButtonAfterResults(long player)
        {
            lock (ActionLock)
            {
                Game.PlayersFinishedRest.Add(player);
                var readyPlayersCount = Game.PlayersFinishedRest.Count;
                UpdateStartRestButton(readyPlayersCount);
            }
        }

        private void UpdateStartRestButton()
        {
            Game.PlayersInGameMode.TryGetValue(GameModeType.Rest, out var readyPlayers);
            var readyPlayersCount = (readyPlayers ?? []).Count;
            UpdateStartRestButton(readyPlayersCount);
        }

        private void UpdateStartRestButton(int readyPlayersCount)
        {
            var totalPlayersCount = GetPlayersCount();
            var isInteractable = readyPlayersCount >= totalPlayersCount;
            GameInteraction.UpdateStartRestButtonState(isInteractable, readyPlayersCount, totalPlayersCount);
        }
    }
}
