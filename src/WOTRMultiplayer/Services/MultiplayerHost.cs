using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Content;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Networking;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.Networking.Messages.Requests;

namespace WOTRMultiplayer.Services
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
            ILevelingInteractionService levelingInteractionService,
            IPlayerNotificationService playerNotificationService,
            IDialogInteractionService dialogInteractionService,
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
                  levelingInteractionService,
                  playerNotificationService,
                  dialogInteractionService,
                  diceRollStorage,
                  fileSystemService,
                  valueGenerator,
                  networkServer)
        {
            _networkServer = networkServer;
        }

        public void Create(string gameId, NetworkGameStartUp gameStartUp)
        {
            if (_networkServer.IsActive)
            {
                _networkServer.Reset();
            }

            SetupNetworkMessageHandlers();

            Game = new NetworkGame(gameStartUp)
            {
                LocalPlayerId = NetworkingConsts.HostPlayerId,
                Id = gameId,
                SessionSeed = CreateRandomSeed()
            };

            Game.Characters.AddRange(gameStartUp.Characters);
            var settings = SettingsService.GetSettings();
            _networkServer.Start(settings.HostPortRangeStart, settings.HostPortRangeEnd, settings.NetworkAwaiterTimeout);

            Logger.LogInformation("Host has been created. IsNewGameSequence={IsNewGameSequence}, SavePath={SavePath}, Portraits={Portraits}", gameStartUp.IsNewGameSequence, gameStartUp.SavePath, string.Join(";", Game.Characters.Select(c => c.Portrait)));
        }

        public void ChangeHostedStartingPoint(string gameId, NetworkGameStartUp gameStartUp)
        {
            Game.Id = gameId;
            Game.StartUp = gameStartUp;
            Game.Characters.Clear();
            Game.Characters.AddRange(gameStartUp.Characters);
            var host = GetHost();
            foreach (var character in Game.Characters)
            {
                character.Owner = host;
            }

            Logger.LogInformation("Notifying game characters changed. Portraits={Portraits}", string.Join(";", Game.Characters.Select(c => c.Portrait)));
            var message = CreateNotifyGameCharactersChanged();
            _networkServer.SendAll(message);

            Logger.LogInformation("Game starting point has been updated. IsNewGameSequence={IsNewGameSequence}, SavePath={SavePath}, Portraits={Portraits}", Game.StartUp.IsNewGameSequence, Game.StartUp.SavePath, string.Join(";", Game.Characters.Select(c => c.Portrait)));
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
                var playersCount = Game.Players.Count;
                if (playersCount <= playerIndex)
                {
                    Logger.LogError("Unable to change character owner as playerIndex is out of range. PlayersCount={PlayersCount}, PlayerIndex={PlayerIndex}", playersCount, playerIndex);
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
            Game = null;

            _networkServer.Reset();
        }

        public bool Start()
        {
            Logger.LogInformation("Starting hosted game...");
            if (!TryGetSaveGameContent(out var content))
            {
                Logger.LogError("Unable to start a game due to missing save file. Path={Path}, IsNewGameSequence={IsNewGameSequence}", Game.StartUp.SavePath, Game.StartUp.IsNewGameSequence);
                return false;
            }

            SetGameStage(NetworkGameStage.PreparingToStart);
            var host = GetHost();
            UpdatePlayerGameStartUpSyncStatus(host, NetworkGameStartUpSyncStatus.Succeed);

            var saveSyncStatusChanged = new NotifyPlayerGameStartUpSyncStatusChanged
            {
                Status = host.StartUpSyncStatus.ToString(),
                PlayerId = host.Id,
            };
            _networkServer.SendAll(saveSyncStatusChanged);

            var saveGameChanged = new NotifyLobbySaveGameChanged
            {
                GameId = Game.Id,
                Content = content,
            };

            Logger.LogInformation("Sending {MessageType}. GameId={GameId}, ContentSize={ContentSize}", nameof(NotifyLobbySaveGameChanged), saveGameChanged.GameId, saveGameChanged.Content?.Length);
            _networkServer.SendAll(saveGameChanged);

            TryStartSavedGame();
            return true;
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
                DialogInteraction.SetDialogContinueButtonState(false);
            }

            if (Game.Dialog == null)
            {
                Logger.LogWarning("Showing dialog cue for not initialized dialog. Most likely it is an autosave load with autostarted dialog after rest. Reinitializing dialog...");
                Game.Dialog = new NetworkDialog(dialogName);
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

            DialogInteraction.ResetSuggestedDialogAnswers();
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

            var canContinue = Game.Combat.PlayersCombatInitialization.Count >= GetSyncedPlayersCount();
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
            var message = new NotifyPerceptionCheckRolled
            {
                Check = Mapper.Map<Networking.Messages.Contracts.NetworkPerceptionCheck>(check)
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, MapObjectId={MapObjectId}, Result={Result}", nameof(NotifyPerceptionCheckRolled), message.Check.UnitId, message.Check.MapObject.Id);

            Send(message);
        }

        public void OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check)
        {
            var message = new NotifyInspectionKnowledgeCheckRolled
            {
                Check = Mapper.Map<Networking.Messages.Contracts.NetworkInspectionKnowledgeCheck>(check)
            };
            Logger.LogInformation("Sending {MessageType}. TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}, StatType={StatType}, DC={DC}",
                nameof(NotifyInspectionKnowledgeCheckRolled), message.Check.TargetUnitId, message.Check.InitiatorUnitId, message.Check.StatType, message.Check.DC);

            Send(message);
        }

        public void OnStealthPerceptionCheckRolled(NetworkStealthPerceptionCheck check)
        {
            var message = new NotifyStealthPerceptionCheckRolled
            {
                Check = Mapper.Map<Networking.Messages.Contracts.NetworkStealthPerceptionCheck>(check)
            };
            Logger.LogInformation("Sending {MessageType}. InitiatorId={InitiatorId}, Roll={Roll}, StealthedUnitId={StealthedUnitId}, IsSuccess={IsSuccess}", nameof(NetworkStealthPerceptionCheck), message.Check.InitiatorId, message.Check.Roll, message.Check.StealthedUnitId, message.Check.IsSuccess);

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

        public void OnAfterTryRollRestRandomEncounter()
        {
            try
            {
                var encounterContext = GameInteraction.RemoteContext?.RandomEncounter;
                if (encounterContext == null)
                {
                    Logger.LogError("Rest random encounter rolling is finished, but context has not been recorded");
                    return;
                }

                Game.Rest.RandomEncounters.Add(encounterContext.Recording);
                Logger.LogInformation("Rest random encounter context has been stored. SleepPhase={SleepPhase}, Data={Data}", Game.Rest.SleepPhase, encounterContext.Recording);

                if (encounterContext.Recording.RandomUnitSeed.HasValue)
                {
                    var settings = SettingsService.GetSettings();
                    EnsureForcePaused(WellKnownKeys.GameNotifications.ForcedPause.RestRandomEncounterLoading.Key, settings.ForcedPauseRandomEncounterTerminationDelay);
                    GameInteraction.UpdateIsInCombatStatus();
                    GameInteraction.SetPause(true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to store rest random encounter context");
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

        public bool OnRequestLevelingUI(string unitId, NetworkLevelingType levelingType)
        {
            lock (ActionLock)
            {
                InitiateLeveling(unitId, levelingType);
                var message = new NotifyCharacterLevelingStarted
                {
                    UnitId = unitId,
                    Type = levelingType.ToString()
                };
                Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, Type={Type}", nameof(NotifyCharacterLevelingStarted), message.UnitId, message.Type);
                Send(message);
            }

            return true;
        }

        public bool TogglePause(bool isPaused)
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

                    ShowForcedPauseReason();

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

        public void OnDialogPopupClosed(NetworkDialogPopup networkDialogPopup)
        {
            ResetPlayersTracker(Game.PlayersInDialogPopup);
            var message = new NotifyDialogPopupClosed
            {
                Popup = Mapper.Map<Networking.Messages.Contracts.NetworkDialogPopup>(networkDialogPopup)
            };
            Logger.LogInformation("Sending {MessageType}, AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", nameof(NotifyDialogPopupClosed), message.Popup.AreaName, message.Popup.DialogName, message.Popup.CueName);
            Send(message);
        }

        public bool OnClickGroupChangerUnit(string unitId)
        {
            var everyoneIsReady = false;
            lock (ActionLock)
            {
                everyoneIsReady = Game.PlayersInGroupChanger.Count >= GetSyncedPlayersCount();
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
            if (Game.ForcedPause != null)
            {
                ShowForcedPauseReason();
                return false;
            }

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

        public void OnCharacterSelectionWindowAccepted()
        {
            ResetPlayersTracker(Game.PlayersInCharacterSelectionWindow);

            var message = new NotifyCharacterSelectionWindowAccepted();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyCharacterSelectionWindowAccepted));
            Send(message);
        }

        public void OnCharacterSelectionWindowClosed()
        {
            ResetPlayersTracker(Game.PlayersInCharacterSelectionWindow);

            var message = new NotifyCharacterSelectionWindowClosed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyCharacterSelectionWindowClosed));
            Send(message);
        }

        public void OnCharacterSelectionToggleChanged(string unitId)
        {
            var message = new NotifyCharacterSelectionToggleChanged
            {
                UnitId = unitId
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}", nameof(NotifyCharacterSelectionToggleChanged), unitId);
            Send(message);
        }

        public void OnNewGameDifficultyChanged(string difficulty)
        {
            var message = new NotifyNewGameDifficultyChanged
            {
                Difficulty = difficulty
            };
            Logger.LogInformation("Sending {MessageType}. Difficulty={Difficulty}", nameof(NotifyNewGameDifficultyChanged), message.Difficulty);

            Send(message);
        }

        public bool OnCreateAndEquipPolymorphInSlot(NetworkPolymorphicItem polymorphicItem)
        {
            var isInParty = IsControlledByPlayers(polymorphicItem.UnitId);
            if (!isInParty)
            {
                return true;
            }

            var message = new NotifyPolymorphicItemCreated
            {
                PolymorphicItem = Mapper.Map<Networking.Messages.Contracts.NetworkPolymorphicItem>(polymorphicItem)
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, ItemName={ItemName}, SlotType={SlotType}", nameof(NotifyPolymorphicItemCreated), message.PolymorphicItem.UnitId, message.PolymorphicItem.Item.Name, message.PolymorphicItem.Position.Type);
            Send(message);

            return true;
        }

        protected override DiceRollValueResponse RetrieveRoll(DiceRollValueRequest rollRequest)
        {
            // the only case when host is retrieving rolls - he is not the turn owner + it's not AI turn
            var character = GetPartyCharacter(Game.Combat.Turn.UnitId);
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
                            PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.HostTurnOrderDesync.Key, player?.Name);

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

        protected override void OnLocalRestGameModeEnded()
        {
            base.OnLocalRestGameModeEnded();

            Game.ForcedPause.ReadyPlayers.Add(Game.LocalPlayerId);
            TryEndForcedPause();
        }

        protected override void OnRemoteRestGameModeEnded(long playerId)
        {
            base.OnRemoteRestGameModeEnded(playerId);

            if (Game.ForcedPause != null)
            {
                lock (ActionLock)
                {
                    Game.ForcedPause.ReadyPlayers.Add(playerId);
                    TryEndForcedPause();
                }
            }
        }

        protected override void OnLocalRestStarted()
        {
            base.OnLocalRestStarted();

            var message = new NotifyRestStarted();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyRestStarted));
            Send(message);
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
               .On<NotifyPlayerGameStartUpSyncStatusChanged>(OnNotifyPlayerSaveGameSyncStatusChanged)
               .On<NotifyPlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
               .On<ClientGameServerConnectionConfirmed>(OnClientGameServerConnectionConfirmed)

               // area transitioning
               .On<ClientAreaLoaded>(OnClientAreaLoaded)

               // leveling
               .On<ClientCharacterLevelingRequested>(OnClientCharacterLevelingRequested)

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

               // inventory
               .On<NotifyPolymorphicItemCreationRequested>(OnNotifyPolymorphicItemCreationRequested)
               ;
        }

        private void OnNotifyPolymorphicItemCreationRequested(long playerId, NotifyPolymorphicItemCreationRequested polymorphicItemCreationRequested)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, ItemName={ItemName}, SlotType={SlotType}", nameof(NotifyPolymorphicItemCreationRequested), playerId, polymorphicItemCreationRequested.PolymorphicItem.UnitId, polymorphicItemCreationRequested.PolymorphicItem.Item.Name, polymorphicItemCreationRequested.PolymorphicItem.Position.Type);

            var polymorphicItem = Mapper.Map<NetworkPolymorphicItem>(polymorphicItemCreationRequested.PolymorphicItem);
            GameInteraction.CreateAndEquipPolymorphicItem(polymorphicItem, createContext: false);

            // clients will receive 'NotifyPolymorphicItemCreated' as a result of polymorphic item creation
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

        private void OnClientCharacterLevelingRequested(long playerId, ClientCharacterLevelingRequested characterLevelingRequested)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, Type={Type}", nameof(ClientCharacterLevelingRequested), playerId, characterLevelingRequested.UnitId, characterLevelingRequested.Type);

            if (!Enum.TryParse<NetworkLevelingType>(characterLevelingRequested.Type, true, out var levelingType))
            {
                Logger.LogError("Invalid char gen screen type value. Value={Value}", characterLevelingRequested.Type);
                return;
            }

            lock (ActionLock)
            {
                if (Game.Leveling != null)
                {
                    Logger.LogWarning("Leveling is already in progress. UnitId={UnitId}, RequestedUnitId={RequestedUnitId}, Type={Type}", Game.Leveling.UnitId, characterLevelingRequested.UnitId, Game.Leveling.Type);
                    var message = new NotifyCharacterLevelingStarted
                    {
                        UnitId = Game.Leveling.UnitId,
                        Type = Game.Leveling.Type.ToString()
                    };
                    Send(message);
                    return;
                }

                LevelingInteraction.StartLeveling(characterLevelingRequested.UnitId, levelingType);
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
            await WaitWhileTrue(() => !timeout.IsCompleted && (Game.Rest?.RandomEncounters.Count ?? 0) < request.SleepPhase,
                $"Rest Random Encounter is not available yet. RequestedSleepPhase={request.SleepPhase}");

            var encounter = Game.Rest?.RandomEncounters.ElementAt(request.SleepPhase - 1);
            var response = new RandomEncounterContextResponse
            {
                Encounter = Mapper.Map<Networking.Messages.Contracts.NetworkRandomEncounter>(encounter)
            };

            Logger.LogInformation("Sending {MessageType}. IsAvailable={IsAvailable}", response.Encounter != null);

            Send(playerId, response);
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

            var hasBeenStarted = await DialogInteraction.StartDialogAsync(requested.DialogName, requested.TargetUnitId, requested.InitiatorUnitId, requested.MapObjectId, requested.SpeakerKey);
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
            DialogInteraction.MarkSuggestedDialogAnswers(suggestions);

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

            var character = GetPartyCharacter(request.UnitId);
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

        private void OnNotifyPlayerSaveGameSyncStatusChanged(long playerId, NotifyPlayerGameStartUpSyncStatusChanged playerSaveGameSyncStatusChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Status={Status}", nameof(NotifyPlayerGameStartUpSyncStatusChanged), playerId, playerSaveGameSyncStatusChanged.Status);

            var status = Mapper.Map<NetworkGameStartUpSyncStatus>(playerSaveGameSyncStatusChanged.Status);
            UpdatePlayerGameStartUpSyncStatus(playerId, status);

            TryStartSavedGame();

            OnAfterNetworkMessageHandled(playerId, playerSaveGameSyncStatusChanged);
        }

        private void OnPlayerReadyStatusChanged(long playerId, NotifyPlayerReadyStatusChanged readyStatusChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, IsReady={IsReady}", nameof(NotifyPlayerReadyStatusChanged), playerId, readyStatusChanged.IsReady);
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

                    InvokeOnPlayersChanged();

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
                if (clientDlc == null || hostDlc.IsAvailable && !clientDlc.IsAvailable)
                {
                    reason = NetworkDiscrepancyReason.Missing;
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

                InvokeOnPlayersChanged();
                var playersChanged = CreateNotifyLobbyPlayersChanged();
                Logger.LogInformation("Sending {MessageType}", nameof(NotifyLobbyPlayersChanged));
                _networkServer.SendAllExcept(playerId, playersChanged);
                ShowPlayerDisconnectedMessage(removedPlayer);

                UpdateRestUIState();

                if (Game.Rest != null)
                {
                    RemovePlayerFromTracker(Game.Rest.PlayersFinishedRest, removedPlayer.Id);
                    UpdateRestResultsUIState();
                }

                TryEnableDialogContinueButton();

                RemovePlayerFromTracker(Game.PlayersInSkipTime, removedPlayer.Id);
                UpdateSkipTimeUIState();

                RemovePlayerFromTracker(Game.PlayersInGroupChanger, removedPlayer.Id);
                UpdateGroupManagerUIState();

                RemovePlayerFromTracker(Game.PlayersInZoneLoot, removedPlayer.Id);
                UpdateZoneLootUIState();

                UpdateRespecWindowStateOnPlayerLeave(removedPlayer.Id);

                UpdateCharacterSelectionUIState();

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
            InvokeOnPlayersChanged();
            Logger.LogInformation("Server has been started. DLCs={DLCs}, Mods={Mods}", hostPlayer.ContentState.DLCs.Count, hostPlayer.ContentState.Mods.Count);
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
            DialogInteraction.SetDialogContinueButtonState(true);
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
                    var allReady = Game.ForcedPause.ReadyPlayers.Count >= GetSyncedPlayersCount();
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

        private void TryStartSavedGame()
        {
            lock (ActionLock)
            {
                var canStart = Game.Stage == NetworkGameStage.PreparingToStart && Game.Players.All(p => p.StartUpSyncStatus == NetworkGameStartUpSyncStatus.Succeed);

                if (canStart)
                {
                    Logger.LogInformation("Everyone is synced, game can be started. Stage={Stage}, IsNewGameSequence={IsNewGameSequence}", Game.Stage, Game.StartUp.IsNewGameSequence);
                    _networkServer.SendAll(new NotifyGameStarted());

                    if (Game.StartUp.IsNewGameSequence)
                    {
                        StartNewGameSequence();
                        return;
                    }

                    LoadSavedGame();
                }
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

        private void ShowForcedPauseReason()
        {
            var pause = Game.ForcedPause;
            if (pause == null)
            {
                return;
            }

            var messageKey = pause.IsLifting ? WellKnownKeys.GameNotifications.ForcedPause.IsLifting.Key : pause.Reason;
            PlayerNotification.ShowWarningNotification(messageKey);
        }

        private bool TryGetSaveGameContent(out byte[] content)
        {
            if (Game.StartUp.IsNewGameSequence)
            {
                content = null;
                return true;
            }

            content = FileSystem.GetRawFileContent(Game.StartUp.SavePath);
            return content != null;
        }
    }
}
