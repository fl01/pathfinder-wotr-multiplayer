using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using WOTRMultiplayer.Entities.Content;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.GlobalMap.Kingdom;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Entities.Units;
using WOTRMultiplayer.Logging.Extensions;
using WOTRMultiplayer.Networking;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.Networking.Messages.Requests;
using WOTRMultiplayer.Services.GameInteraction.Contexts;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.Services
{
    public class MultiplayerHost : MultiplayerActorBase, IMultiplayerHost
    {
        private readonly INetworkServer _networkServer;

        private NetworkLobbyStage Status => Game?.Stage ?? NetworkLobbyStage.None;

        public Action<NetworkGameConnectivity> OnConnected { get; set; }

        public bool IsActive => _networkServer.IsActive;

        public bool IsInLobby => IsActive && Status == NetworkLobbyStage.Lobby;

        protected override bool HasControlOverUI => true;

        public MultiplayerHost(
            ILogger<MultiplayerHost> logger,
            IGameInteractionService gameInteractionService,
            ILevelingInteractionService levelingInteractionService,
            IPlayerNotificationService playerNotificationService,
            IDialogInteractionService dialogInteractionService,
            IGlobalMapInteractionService globalMapInteractionService,
            IPingInteractionService pingInteractionService,
            ICombatInteractionService combatInteractionService,
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
                  globalMapInteractionService,
                  pingInteractionService,
                  combatInteractionService,
                  diceRollStorage,
                  fileSystemService,
                  valueGenerator,
                  networkServer)
        {
            _networkServer = networkServer;

            SetupNetworkMessageHandlers();
        }

        public void Create(string gameId, NetworkGameStartUp gameStartUp)
        {
            if (_networkServer.IsActive)
            {
                _networkServer.Reset();
            }

            Game = new NetworkGame(gameStartUp)
            {
                LocalPlayerId = NetworkingConsts.HostPlayerId,
                Id = gameId,
                SessionSeed = CreateRandomSeed()
            };

            Game.Characters.AddRange(gameStartUp.Characters);

            var settings = SettingsService.GetSettings();
            _networkServer.Start(settings.HostPortRangeStart, settings.HostPortRangeEnd, settings.NetworkAwaiterTimeout);

            OnCharactersChanged?.Invoke(Game.StartUp.Title, Game.Characters);
            Logger.LogInformation("Host has been created. GameId={GameId}, IsNewGameSequence={IsNewGameSequence}, SavePath={SavePath}, Portraits={Portraits}", Game.Id, gameStartUp.IsNewGameSequence, gameStartUp.SavePath, string.Join(";", Game.Characters.Select(c => c.Portrait)));
        }

        public void ChangeHostedStartingPoint(string gameId, NetworkGameStartUp gameStartUp)
        {
            Game.Id = gameId;
            Game.StartUp = gameStartUp;
            Game.Characters.Clear();
            Game.Characters.AddRange(gameStartUp.Characters);

            ResetCharacterOwnership();
            var charactersChanged = new NotifyLobbyCharactersChanged
            {
                Title = Game.StartUp.Title,
                Characters = Mapper.Map<List<Networking.Messages.Contracts.NetworkCharacter>>(Game.Characters)
            };
            Send(charactersChanged);

            OnCharactersChanged?.Invoke(Game.StartUp.Title, Game.Characters);
            Logger.LogInformation("Game starting point has been updated. GameId={GameId}, IsNewGameSequence={IsNewGameSequence}, SavePath={SavePath}, Portraits={Portraits}", Game.Id, Game.StartUp.IsNewGameSequence, Game.StartUp.SavePath, string.Join(";", Game.Characters.Select(c => c.Portrait)));
        }

        public void ChangeCharacterOwner(NetworkCharacter character, NetworkPlayer player)
        {
            lock (ActionLock)
            {
                var actualPlayer = GetPlayer(player.Id);
                if (actualPlayer == null)
                {
                    Logger.LogWarning("Unable to change character owner for missed player. PlayerId={PlayerId}", player.Id);
                    return;
                }

                var actualCharacter = FindCharacter(character);
                if (actualCharacter.Owner?.Id == actualPlayer.Id)
                {
                    return;
                }

                actualCharacter.Owner = actualPlayer;
                Logger.LogInformation("New character owner. CharacterName={CharacterName}, PlayerId={PlayerId}, PlayerName={PlayerName}", actualCharacter.Name, actualPlayer.Id, actualPlayer.Name);

                // UnitId becomes relevant once we are in the game
                if (!string.IsNullOrEmpty(actualCharacter.UnitId))
                {
                    UpdateCharacterOwnershipHistory(actualCharacter);
                }

                var characterOwnerChanged = new NotifyCharacterOwnerChanged
                {
                    Character = Mapper.Map<Networking.Messages.Contracts.NetworkCharacter>(actualCharacter)
                };
                Send(characterOwnerChanged);

                UpdateInGameCharacterOwnershipChange(actualCharacter);
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

            SetLobbyStage(NetworkLobbyStage.PreparingToStart);
            Game.LoadedSaveSeed = CreateRandomSeed();

            var host = GetHost();
            UpdateLobbySyncStatus(host, NetworkLobbySyncStatus.Succeed);

            var saveSyncStatusChanged = new NotifyLobbySyncStatusChanged
            {
                Status = host.LobbySyncStatus.ToString(),
                PlayerId = host.Id,
            };
            Send(saveSyncStatusChanged);

            var saveGameChanged = new NotifyLobbySaveGameChanged
            {
                GameId = Game.Id,
                Content = content,
                Seed = Game.LoadedSaveSeed
            };
            Send(saveGameChanged);

            TryStartSavedGame();
            return true;
        }

        public override void OnAreaLoaded()
        {
            base.OnAreaLoaded();

            var areaSeed = CreateRandomSeed();
            SetAreaSeed(areaSeed);

            TryEndForcedPause(Game.LocalPlayerId);
        }

        public void OnAreaTransition(NetworkAreaTransition areaTransition)
        {
            var message = new NotifyPartyAreaTransitioned
            {
                Transition = Mapper.Map<Networking.Messages.Contracts.NetworkAreaTransition>(areaTransition)
            };
            Send(message);
        }

        public void OnAfterCueShow(NetworkDialog networkDialog, string cueName, bool hasSystemAnswer)
        {
            Logger.LogInformation("Showing dialog Cue. DialogId={DialogId}, DialogName={DialogName}, CueName={CueName}, HasSystemAnswer={HasSystemAnswer}", networkDialog.Id, networkDialog.Name, cueName, hasSystemAnswer);
            if (hasSystemAnswer)
            {
                DialogInteraction.SetDialogContinueButtonState(false);
            }

            if (Game.DialogState == null)
            {
                Logger.LogWarning("Showing dialog cue for not initialized dialog. Most likely it is an autosave load with autostarted dialog after rest. Reinitializing dialog...");
                Game.DialogState = new NetworkDialogState(networkDialog);
            }

            Game.DialogState.CurrentCueName = cueName;
            AddCueWitness(cueName, Game.LocalPlayerId);

            TryEnableDialogContinueButton();
        }

        public bool OnBeforeSelectDialogAnswer(NetworkDialog networkDialog, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId)
        {
            Logger.LogInformation("Select Dialog Answer. DialogId={DialogId}, DialogName={DialogName}, CueName={CueName} Answer={Answer}, IsExitAnswer={IsExitAnswer}, ManualUnitSelectionId={ManualUnitSelectionId}",
                networkDialog.Id, networkDialog.Name, cueName, answerName, isExitAnswer, manualUnitSelectionId);

            var missingPlayers = GetPlayersWhoHaveNotSeenCueYet(cueName);
            if (missingPlayers.Count > 0)
            {
                Logger.LogWarning("Some players haven't seen the dialog yet. Players={Players}", string.Join(";", missingPlayers.Select(p => p.Name)));
                DialogInteraction.PlayUnableToSelectCueAnimation(answerName);
                PlayerNotification.ShowWarningNotification(WellKnownKeys.GameNotifications.Dialogs.WaitingForOtherPlayers.Key, addToLog: false);
                return false;
            }

            DialogInteraction.ResetSuggestedDialogAnswers();
            Game.DialogState.AnswerSuggestions.Clear();
            Game.DialogState.CueViews.TryRemove(cueName, out _);
            Game.DialogState.Answer = new NetworkDialogAnswer
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
            if (Game.DialogState == null)
            {
                Logger.LogError("Unable to send dialog answer because dialog is null");
                return;
            }

            if (Game.DialogState.Answer == null)
            {
                return;
            }

            var message = new NotifyDialogCueAnswerSelected
            {
                Dialog = Mapper.Map<Networking.Messages.Contracts.NetworkDialog>(Game.DialogState.Dialog),
                CueName = Game.DialogState.Answer.CueName,
                AnswerName = Game.DialogState.Answer.AnswerName,
                ManualUnitSelectionId = Game.DialogState.Answer.ManualUnitSelectionId
            };

            Send(message);
            Game.DialogState.Answer = null;
        }

        public bool StartDialog(NetworkDialog networkDialog)
        {
            if (!string.Equals(Game.DialogState?.Dialog.Id, networkDialog.Id, StringComparison.OrdinalIgnoreCase))
            {
                Game.DialogState = new NetworkDialogState(networkDialog);
            }

            if (!Game.DialogState.Dialog.IsScripted)
            {
                var message = new NotifyDialogStarted
                {
                    Dialog = Mapper.Map<Networking.Messages.Contracts.NetworkDialog>(networkDialog)
                };
                Send(message);
            }

            Logger.LogInformation("Dialog has been started. DialogId={DialogId}, DialogName={Name}, IsScripted={IsScripted}", Game.DialogState.Dialog.Id, Game.DialogState.Dialog.Name, Game.DialogState.Dialog.IsScripted);
            return true;
        }

        /// <summary>
        /// 35 - UnitCombatPrepareController
        /// </summary>
        /// <returns></returns>
        public bool CanInitializeCombat()
        {
            if (Game.Combat == null)
            {
                return false;
            }

            switch (Game.Combat.Stage)
            {
                case NetworkCombatStage.Idle:
                    if (!Game.Combat.PlayersCombatPreparation.TryGetValue(Game.LocalPlayerId, out _))
                    {
                        var unitsInCombat = CombatInteraction.GetUnitsInCombat();
                        Game.Combat.PlayersCombatPreparation.TryAdd(Game.LocalPlayerId, unitsInCombat);
                    }

                    var canStartPreparing = Game.Combat.PlayersCombatPreparation.Count >= GetSyncedPlayersCount();
                    if (canStartPreparing)
                    {
                        SetCombatStage(NetworkCombatStage.Preparing);
                    }
                    else
                    {
                        if (!Game.Combat.IsRecovering && (DateTime.UtcNow - Game.Combat.StartedAt) > TimeSpan.FromSeconds(15))
                        {
                            var player = GetPlayer(Game.LocalPlayerId);
                            InitiateCombatRecovering(player);
                        }
                    }
                    return false;
                case NetworkCombatStage.Preparing:
                    if (CombatInteraction.IsAnyProjectilesLaunchedByParty())
                    {
                        return false;
                    }

                    if (!Game.Combat.IsPreparationStarted)
                    {
                        Game.Combat.Seed = CreateRandomSeed();

                        var discrepantUnits = GetDiscrepantCombatUnits();
                        var preparationRequiredMessage = new NotifyCombatPreparationRequired
                        {
                            Discrepancy = Mapper.Map<Networking.Messages.Contracts.NetworkCombatUnitDiscrepancy>(discrepantUnits),
                        };
                        Send(preparationRequiredMessage);
                        Game.Combat.IsPreparationStarted = true;
                        Task.Factory.StartNew(() =>
                            FixCombatUnitDiscrepancyAsync(discrepantUnits)
                            .ContinueWith(_ => Game.Combat.IsPrepared = true));
                    }

                    var isPrepared = Game.Combat.IsPrepared && Game.Combat.PlayersCombatPreparation.Count == 0;
                    if (isPrepared)
                    {
                        SetCombatStage(NetworkCombatStage.Initialization);
                    }
                    return false;
                default:
                    return true;
            }
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

            switch (Game.Combat.Stage)
            {
                case NetworkCombatStage.Idle:
                case NetworkCombatStage.Preparing:
                    return false;
                case NetworkCombatStage.Initialization:
                    if (!Game.Combat.IsInitialized)
                    {
                        var combatState = CombatInteraction.GetCombatState();
                        var message = new NotifyCombatInitializationRequired
                        {
                            State = Mapper.Map<Networking.Messages.Contracts.NetworkCombatState>(combatState),
                            CombatSeed = Game.Combat.Seed,
                            TriggeredAreaEffects = Mapper.Map<List<Networking.Messages.Contracts.NetworkAreaEffect>>(Game.Combat.TriggeredAreaEffects)
                        };
                        Game.Combat.TriggeredAreaEffects.Clear();
                        Send(message);

                        Game.Combat.IsRecovering = false;
                        Game.Combat.IsInitialized = true;
                        Game.Combat.PlayersCombatInitialization.TryAdd(Game.LocalPlayerId, true);
                    }

                    var canContinue = Game.Combat.PlayersCombatInitialization.Count >= GetSyncedPlayersCount();
                    if (canContinue)
                    {
                        SetCombatStage(NetworkCombatStage.Playing);
                    }
                    return false;
                case NetworkCombatStage.Playing:
                    if (!Game.Combat.IsPlaying)
                    {
                        var message = new NotifyCombatInitializationCompleted();
                        Send(message);
                        Game.Combat.IsPlaying = true;
                    }
                    return true;
                default:
                    return Game.Combat.IsPlaying;
            }
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
            Send(message);
        }

        public void OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check)
        {
            var message = new NotifyInspectionKnowledgeCheckRolled
            {
                Check = Mapper.Map<Networking.Messages.Contracts.NetworkInspectionKnowledgeCheck>(check)
            };
            Send(message);
        }

        public void OnStealthPerceptionCheckRolled(NetworkStealthPerceptionCheck check)
        {
            var message = new NotifyStealthPerceptionCheckRolled
            {
                Check = Mapper.Map<Networking.Messages.Contracts.NetworkStealthPerceptionCheck>(check)
            };
            Send(message);
        }

        public bool OnSpawnCampPlace(NetworkVector3 position)
        {
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
            Send(message);
        }

        public void OnCampingStateChanged(NetworkCampingState state)
        {
            var message = new NotifyCampingStateChanged
            {
                State = Mapper.Map<Networking.Messages.Contracts.NetworkCampingState>(state)
            };
            Send(message);
        }

        public void OnCampingUnitsRoleChanged(List<NetworkCampingRole> roles)
        {
            var message = new NotifyCampingUnitsRoleChanged
            {
                Roles = Mapper.Map<List<Networking.Messages.Contracts.NetworkCampingRole>>(roles),
            };
            Send(message);
        }

        public void OnAfterTryRollRestRandomEncounter(NetworkRandomEncounterContext encounterContext)
        {
            try
            {
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
                    lock (ActionLock)
                    {
                        EnsureForcePaused(NetworkForcedPauseReason.RestEncounterLoading, settings.ForcedPauseRandomEncounterTerminationDelay);
                    }
                    GameInteraction.SetPause(true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to store rest random encounter context");
                throw;
            }
        }

        public void OnMakeVendorDeal()
        {
            var message = new NotifyVendorDealMade();
            Send(message);
        }

        public void OnCloseVendorWindow()
        {
            var message = new NotifyVendorWindowClosed();
            Send(message);
        }

        public bool OnRequestLevelingUI(string unitId, NetworkLevelingType levelingType)
        {
            lock (ActionLock)
            {
                if (Game.Leveling == null)
                {
                    InitiateLeveling(unitId, levelingType);
                    SendLevelingStartedConfirmation();
                    return true;
                }

                Logger.LogWarning("New leveling has been denied as another one is progress. UnitId={UnitId}, Type={Type}", Game.Leveling.UnitId, Game.Leveling.Type);
                return false;
            }
        }

        public void OnAutoPausedByTrapDetection()
        {
            lock (ActionLock)
            {
                EnsureForcePaused(NetworkForcedPauseReason.TrapDetected, removalDelay: null);
                Game.ForcedPause.ReadyPlayers.Add(Game.LocalPlayerId);
            }
        }

        public void OnCloseGroupChangerPartyUI()
        {
            ResetPlayersTracker(Game.PlayersInGroupChanger);

            var message = new NotifyGroupChangerClosed();
            Send(message);
        }

        public void OnDialogPopupAccepted(NetworkDialogPopup networkDialogPopup)
        {
            ResetPlayersTracker(Game.PlayersInDialogPopup);
            var message = new NotifyDialogPopupAccepted
            {
                Popup = Mapper.Map<Networking.Messages.Contracts.NetworkDialogPopup>(networkDialogPopup)
            };
            Send(message);
        }

        public void OnDialogPopupClosed(NetworkDialogPopup networkDialogPopup)
        {
            ResetPlayersTracker(Game.PlayersInDialogPopup);
            var message = new NotifyDialogPopupClosed
            {
                Popup = Mapper.Map<Networking.Messages.Contracts.NetworkDialogPopup>(networkDialogPopup)
            };
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
            Send(message);

            return true;
        }

        public void OnAcceptGroupChangerParty()
        {
            var message = new NotifyGroupChangerPartyAccepted();
            Send(message);
            ResetPlayersTracker(Game.PlayersInGroupChanger);
        }

        public void OnGlobalMapRestOpened()
        {
            var message = new NotifyGlobalMapRestOpened();
            Send(message);
        }

        public void OnRestWindowClosed()
        {
            var message = new NotifyRestWindowClosed();
            Send(message);
        }

        public void OnGlobalMapGroupChangerOpened()
        {
            var message = new NotifyGlobalMapGroupChangerOpened();
            Send(message);
        }

        public void OnGlobalMapTravelStarted(NetworkGlobalMapTravel globalMapTravel)
        {
            var party = CombatInteraction.GetParty();
            var message = new NotifyGlobalMapTravelStarted
            {
                Travel = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapTravel>(globalMapTravel),
                Party = Mapper.Map<List<Networking.Messages.Contracts.NetworkUnit>>(party)
            };
            Send(message);
        }

        public void OnGlobalMapSelectedArmyChanged(NetworkGlobalMapArmy globalMapArmy)
        {
            var message = new NotifyGlobalMapSelectedArmyChanged
            {
                Army = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmy>(globalMapArmy)
            };
            Send(message);
        }

        public void OnGlobalMapAutoCrusadeCombatChanged(bool isEnabled)
        {
            var message = new NotifyGlobalMapAutoCrusadeCombatChanged
            {
                IsEnabled = isEnabled
            };
            Send(message);
        }

        public void OnSkipTimeClosed()
        {
            ResetPlayersTracker(Game.PlayersInSkipTime);

            var message = new NotifySkipTimeClosed();
            Send(message);
        }

        public void OnSkipTimeHoursChanged(float hours)
        {
            var message = new NotifySkipTimeHoursChanged
            {
                Hours = hours
            };
            Send(message);
        }

        public void OnSkipTimeStarted()
        {
            var message = new NotifySkipTimeStarted();
            Send(message);
        }

        public bool OnGlobalMapSelectLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            var readyPlayers = GetPlayersCountWithSyncedGlobalMapMode();
            if (!readyPlayers.HasValue)
            {
                Logger.LogError("Global Map location select has been denied due to invalid ready players value");
                return false;
            }

            var canSelect = readyPlayers.Value >= GetSyncedPlayersCount();
            if (!canSelect)
            {
                PlayerNotification.ShowWarningNotification(WellKnownKeys.GameNotifications.ForcedPause.AreaLoading.Key);
            }

            return canSelect;
        }

        public void OnGlobalMapContinueTravel(NetworkGlobalMapTraveler globalMapTraveler)
        {
            var party = CombatInteraction.GetParty();
            var message = new NotifyGlobalMapTravelContinued
            {
                Traveler = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapTraveler>(globalMapTraveler),
                Party = Mapper.Map<List<Networking.Messages.Contracts.NetworkUnit>>(party)
            };
            Send(message);
        }

        public void OnGlobalMapStopTravel(NetworkGlobalMapTraveler globalMapTraveler)
        {
            var party = CombatInteraction.GetParty();
            var message = new NotifyGlobalMapTravelStopped
            {
                Traveler = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapTraveler>(globalMapTraveler),
                Party = Mapper.Map<List<Networking.Messages.Contracts.NetworkUnit>>(party)
            };
            Send(message);
        }

        public void OnGlobalMapCommonPopupAccepted(NetworkGlobalMapCommonPopup globalMapCommonPopup)
        {
            var message = new NotifyGlobalMapCommonPopupAccepted
            {
                Popup = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapCommonPopup>(globalMapCommonPopup)
            };
            Send(message);
        }

        public void OnGlobalMapEncounterAccepted()
        {
            var message = new NotifyGlobalMapEncounterAccepted();
            Send(message);
        }

        public void OnGlobalMapEncounterAvoided()
        {
            var message = new NotifyGlobalMapEncounterAvoided();
            Send(message);
        }

        public void OnGlobalMapRandomEncounterRolled(NetworkGlobalMapEncounter globalMapEncounter)
        {
            var message = new NotifyGlobalMapEncounterRolled
            {
                Encounter = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapEncounter>(globalMapEncounter)
            };
            Send(message);
        }

        public void OnGlobalMapSkipDay()
        {
            var message = new NotifyGlobalMapDaySkipped();
            Send(message);
        }

        public void OnZoneLootRemoveToggleChanged(bool removeUncollectedLoot)
        {
            var message = new NotifyZoneLootRemoveToggleChanged
            {
                RemoveLoot = removeUncollectedLoot
            };
            Send(message);
        }

        public void OnZoneLootCompleted()
        {
            var message = new NotifyZoneLootCompleted();
            Send(message);
        }

        public void OnZoneLootLeft()
        {
            var message = new NotifyZoneLootLeft();
            Send(message);
        }

        public void OnCharacterSelectionWindowAccepted()
        {
            ResetPlayersTracker(Game.PlayersInCharacterSelectionWindow);

            var message = new NotifyCharacterSelectionWindowAccepted();
            Send(message);
        }

        public void OnCharacterSelectionWindowClosed()
        {
            ResetPlayersTracker(Game.PlayersInCharacterSelectionWindow);

            var message = new NotifyCharacterSelectionWindowClosed();
            Send(message);
        }

        public void OnCharacterSelectionToggleChanged(string unitId)
        {
            var message = new NotifyCharacterSelectionToggleChanged
            {
                UnitId = unitId
            };
            Send(message);
        }

        public void OnNewGameDifficultyChanged(string difficulty)
        {
            var message = new NotifyNewGameDifficultyChanged
            {
                Difficulty = difficulty
            };
            Send(message);
        }

        public bool OnCreateAndEquipPolymorphInSlot(NetworkPolymorphicItem polymorphicItem)
        {
            var isInParty = IsControlledByPlayers(polymorphicItem.UnitId) || GameInteraction.IsUnitInParty(polymorphicItem.UnitId);
            if (!isInParty)
            {
                return true;
            }

            var message = new NotifyPolymorphicItemCreated
            {
                PolymorphicItem = Mapper.Map<Networking.Messages.Contracts.NetworkPolymorphicItem>(polymorphicItem)
            };
            Send(message);

            return true;
        }

        public void OnCrusadeArmyBattleResultsClosed()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyBattleResults);
            var message = new NotifyCrusadeArmyBattleResultsClosed();
            Send(message);
        }

        public void OnCrusadeArmyBattleResultsManualCombatStarted()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyBattleResults);
            var message = new NotifyCrusadeArmyBattleResultsManualCombatStarted();
            Send(message);
        }

        public void OnGlobalMapLocationMessageAccepted()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapLocationMessage);
            var message = new NotifyGlobalMapLocationMessageAccepted();
            Send(message);
        }

        public void OnGlobalMapLocationMessageClosed()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapLocationMessage);

            var message = new NotifyGlobalMapLocationMessageClosed
            {
                PlayerId = Game.LocalPlayerId
            };
            Send(message);

            UpdateGlobalMapLocationMessageUIState();
        }

        public void OnGlobalMapCommonPopupDeclined(NetworkGlobalMapCommonPopup globalMapCommonPopup)
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCommonPopup, Game.LocalPlayerId);

            var message = new NotifyGlobalMapCommonPopupDeclined
            {
                PlayerId = Game.LocalPlayerId,
                Popup = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapCommonPopup>(globalMapCommonPopup)
            };
            Send(message);
        }

        public void OnGlobalMapCombatResultsClosed()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCombatResults);
            var message = new NotifyGlobalMapCombatResultsClosed();
            Send(message);
        }

        public bool OnTacticalCombatInitialization()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCombatResults);

            Game.ArmyCombat = new NetworkArmyCombat
            {
                IsInitialized = false,
                Seed = CreateRandomSeed()
            };
            return true;
        }

        public void OnTacticalCombatInitialized()
        {
            Game.ArmyCombat.PlayersCombatInitialization.TryAdd(Game.LocalPlayerId, true);
            Game.ArmyCombat.IsInitialized = TryConfirmTacticalCombatInitialization();
            Game.ArmyCombat.AreaSeed = CombatInteraction.GetCrusadeArmyCombatAreaSeed();

            var message = new NotifyTacticalCombatInitialized()
            {
                AreaSeed = Game.ArmyCombat.AreaSeed,
                Seed = Game.ArmyCombat.Seed
            };
            Send(message);
        }

        public void OnTacticalCombatUnitUseAbilityCommand(NetworkTacticalUnitUseAbilityCommand tacticalUnitUseAbilityCommand)
        {
            var message = new NotifyTacticalUnitUseAbilityCommandExecuted
            {
                Command = Mapper.Map<Networking.Messages.Contracts.NetworkTacticalUnitUseAbilityCommand>(tacticalUnitUseAbilityCommand)
            };
            Send(message);
        }

        public void OnTacticalCombatUnitAttackCommand(NetworkTacticalUnitAttackCommand tacticalUnitAttackCommand)
        {
            var message = new NotifyTacticalUnitAttackCommandExecuted
            {
                Command = Mapper.Map<Networking.Messages.Contracts.NetworkTacticalUnitAttackCommand>(tacticalUnitAttackCommand)
            };
            Send(message);
        }

        public void OnTacticalCombatUnitMoveToCommand(NetworkTacticalUnitMoveToCommand tacticalUnitMoveToCommand)
        {
            var message = new NotifyTacticalUnitMoveToCommandExecuted
            {
                Command = Mapper.Map<Networking.Messages.Contracts.NetworkTacticalUnitMoveToCommand>(tacticalUnitMoveToCommand)
            };
            Send(message);
        }

        public bool OnTacticalCombatTotalDefenseUsed()
        {
            var message = new NotifyTacticalCombatTotalDefenseUsed();
            Send(message);
            return true;
        }

        public bool OnTacticalCombatTurnPostponed()
        {
            var message = new NotifyTacticalCombatTurnPostponed();
            Send(message);
            return true;
        }

        public void OnTacticalCombatRetreat()
        {
            var message = new NotifyTacticalCombatRetreated();
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyInfoClosed()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyInfo);

            var message = new NotifyGlobalMapCrusadeArmyInfoClosed();
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyMoveSquadsToMainArmy()
        {
            var message = new NotifyGlobalMapCrusadeArmySquadsMovedToMainArmy();
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyMoveSquadsToSecondArmy()
        {
            var message = new NotifyGlobalMapCrusadeArmySquadsMovedToSecondArmy();
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyInfoNextMergeArmy()
        {
            var message = new NotifyGlobalMapCrusadeArmyInfoNextMergeArmySelected();
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyInfoPrevMergeArmy()
        {
            var message = new NotifyGlobalMapCrusadeArmyInfoPrevMergeArmySelected();
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyLeaderAction(NetworkGlobalMapArmyLeader globalMapArmyLeader, NetworkGlobalMapArmyLeaderActionType armyLeaderActionType)
        {
            var message = new NotifyGlobalMapCrusadeArmyLeaderActionExecuted()
            {
                Type = armyLeaderActionType.ToString(),
                Leader = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmyLeader>(globalMapArmyLeader)
            };
            Send(message);
        }

        public void OnGlobalMapMergeArmies()
        {
            var message = new NotifyGlobalMapCrusadeArmiesMerging();
            Send(message);
        }

        public void OnGlobalMapCreateCrusadeArmy()
        {
            var message = new NotifyGlobalMapCrusadeArmyCreated();
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyMainCartClosed()
        {
            var message = new NotifyGlobalMapCrusadeArmyMainCartClosed();
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyRecruitCartClosed()
        {
            var message = new NotifyGlobalMapCrusadeArmyRecruitCartClosed();
            Send(message);
        }

        public void OnGlobalMapRecruitmentBuyUnits(NetworkGlobalMapUnitRecruitmentOrder globalMapUnitRecruitmentOrder)
        {
            var message = new NotifyGlobalMapUnitsRecruited
            {
                Order = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapUnitRecruitmentOrder>(globalMapUnitRecruitmentOrder)
            };
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyDismiss(NetworkGlobalMapArmy globalMapArmy)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCommonPopup);

            var message = new NotifyGlobalMapCrusadeArmyDismissed
            {
                Army = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmy>(globalMapArmy),
            };
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyLeaderLevelingClosed()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling);
            var message = new NotifyGlobalMapCrusadeArmyLeaderLevelingClosed();
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyLeaderLevelingConfirmed()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling);
            var message = new NotifyGlobalMapCrusadeArmyLeaderLevelingConfirmed();
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyLeaderLevelingSkillSelected(string skillId)
        {
            var message = new NotifyGlobalMapCrusadeArmyLeaderLevelingSkillSelected
            {
                Id = skillId
            };
            Send(message);
        }

        public void OnKingdomEventSelected(NetworkKingdomEvent kingdomEvent)
        {
            var message = new NotifyKingdomEventSelected
            {
                Event = Mapper.Map<Networking.Messages.Contracts.NetworkKingdomEvent>(kingdomEvent)
            };
            Send(message);
        }

        public void OnKingdomEventSolutionSelected(NetworkKingdomEventSolution kingdomEventSolution)
        {
            var message = new NotifyKingdomEventSolutionSelected
            {
                Solution = Mapper.Map<Networking.Messages.Contracts.NetworkKingdomEventSolution>(kingdomEventSolution)
            };
            Send(message);
        }

        public void OnKingdomEventStarted()
        {
            var message = new NotifyKingdomEventStarted();
            Send(message);
        }

        public void OnKingdomEventCancelled()
        {
            var message = new NotifyKingdomEventCancelled();
            Send(message);
        }

        public void OnKingdomEventDropped(NetworkKingdomEvent kingdomEvent)
        {
            var message = new NotifyKingdomEventDropped
            {
                Event = Mapper.Map<Networking.Messages.Contracts.NetworkKingdomEvent>(kingdomEvent)
            };
            Send(message);
        }

        public void OnKingdomUpgradeSettlement(NetworkKingdomSettlement kingdomSettlement)
        {
            var message = new NotifyKingdomSettlementUpgraded
            {
                Settlement = Mapper.Map<Networking.Messages.Contracts.NetworkKingdomSettlement>(kingdomSettlement)
            };
            Send(message);
        }

        public void OnKingdomEnterSettlement(NetworkKingdomSettlement kingdomSettlement, bool requiresUnloadEvent, bool exitSettlementToGlobalMap)
        {
            var message = new NotifyKingdomSettlementEntered
            {
                Settlement = Mapper.Map<Networking.Messages.Contracts.NetworkKingdomSettlement>(kingdomSettlement),
                RequiresUnloadEvent = requiresUnloadEvent,
                ExitSettlementToGlobalMap = exitSettlementToGlobalMap
            };
            Send(message);
        }

        public void OnKingdomLeaveSettlement()
        {
            var message = new NotifyKingdomSettlementLeft();
            Send(message);
        }

        public void OnKingdomSettlementBuldingSold(NetworkKingdomSettlementBuilding kingdomSettlementBuilding)
        {
            var message = new NotifyKingdomSettlementBuildingSold
            {
                Building = Mapper.Map<Networking.Messages.Contracts.NetworkKingdomSettlementBuilding>(kingdomSettlementBuilding)
            };
            Send(message);
        }

        public void OnKingdomSettlementBuilt(NetworkKingdomSettlementBuilding kingdomSettlementBuilding)
        {
            var message = new NotifyKingdomSettlementBuildingBuilt
            {
                Building = Mapper.Map<Networking.Messages.Contracts.NetworkKingdomSettlementBuilding>(kingdomSettlementBuilding)
            };
            Send(message);
        }

        public NetworkAIAction OnAfterAISelectedAction(NetworkAIAction aiAction)
        {
            var message = new NotifyAIActionSelected
            {
                Action = Mapper.Map<Networking.Messages.Contracts.NetworkAIAction>(aiAction)
            };
            Send(message);
            return null;
        }

        public void OnGlobalMapMagicSpellUsed(NetworkGlobalMapMagicSpell globalMagicSpell)
        {
            var message = new NotifyGlobalMapMagicSpellUsed
            {
                Spell = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapMagicSpell>(globalMagicSpell),
            };
            Send(message);
        }

        public void OnGlobalMapRecruitmentBuyResources(NetworkGlobalMapResourceOrder globalMapResourceOrder)
        {
            var message = new NotifyGlobalMapResourcesBought
            {
                Order = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapResourceOrder>(globalMapResourceOrder)
            };
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyCartNameChanged(NetworkGlobalMapArmy globalMapArmy)
        {
            var message = new NotifyGlobalMapCrusadeArmyInfoCartNameChanged
            {
                Army = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmy>(globalMapArmy)
            };
            Send(message);
        }

        public void OnGlobalMapCrusadeArmySetLeaderClear()
        {
            var message = new NotifyGlobalMapCrusadeArmySetLeaderClearClicked();
            Send(message);
        }

        public void OnGlobalMapCrusadeArmySetLeaderRecruit()
        {
            var message = new NotifyGlobalMapCrusadeArmySetLeaderRecruitClicked();
            Send(message);
        }

        public void OnGlobalMapRecruitmentMercReroll()
        {
            var message = new NotifyGlobalMapRecruitmentMercenariesRerolled();
            Send(message);
        }

        public void OnGlobalMapRecruitmentNextArmy()
        {
            var message = new NotifyGlobalMapRecruitmentNextArmySelected();
            Send(message);
        }

        public void OnGlobalMapRecruitmentPrevArmy()
        {
            var message = new NotifyGlobalMapRecruitmentPrevArmySelected();
            Send(message);
        }

        public void OnGlobalMapCrusadeArmySquadDismiss(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot)
        {
            var message = new NotifyGlobalMapCrusadeArmySquadDismissed
            {
                SquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(globalMapArmySquadSlot),
            };
            Send(message);
        }

        public bool OnGlobalMapCrusadeArmyMergedInOne(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot)
        {
            var message = new NotifyGlobalMapCrusadeArmyMergedInOne
            {
                SquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(globalMapArmySquadSlot),
            };
            Send(message);
            return true;
        }

        public bool OnGlobalMapCrusadeArmySquadSplitted(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot, int count)
        {
            var message = new NotifyGlobalMapCrusadeArmySquadSplitted
            {
                SquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(globalMapArmySquadSlot),
                Count = count
            };
            Send(message);
            return true;
        }

        public void OnGlobalMapCrusadeArmySquadsMerged(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count)
        {
            var message = new NotifyGlobalMapCrusadeArmySquadsMerged
            {
                SourceSquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(sourceSquadSlot),
                TargetSquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(targetSquadSlot),
                Count = count
            };
            Send(message);
        }

        public void OnGlobalMapCrusadeArmySquadsSwitched(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot)
        {
            var message = new NotifyGlobalMapCrusadeArmySquadsSwitched
            {
                SourceSquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(sourceSquadSlot),
                TargetSquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(targetSquadSlot),
            };
            Send(message);
        }

        public void OnGlobalMapCrusadeArmySquadSplitRequested(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count)
        {
            var message = new NotifyGlobalMapCrusadeArmySquadSplitRequested
            {
                SourceSquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(sourceSquadSlot),
                TargetSquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(targetSquadSlot),
                Count = count
            };
            Send(message);
        }

        public override void OnStartRest()
        {
            base.OnStartRest();

            var message = new NotifyRestStarted();
            Send(message);
        }

        public bool OnAreaEffectTriggered(NetworkAreaEffect areaEffect)
        {
            if (Game.Combat != null && Game.Combat.Turn == null && Game.Combat.TriggeredAreaEffects.Add(areaEffect))
            {
                Logger.LogWarning("Area effect has been triggered in combat mid turn. Id={Id}, Name={Name}", areaEffect.Id, areaEffect.Name);
                PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.AreaEffects.Triggered.Key, Abstractions.GameInteraction.CombatLog.CombatTextSeverity.Debug, areaEffect.Name, areaEffect.Id);
            }

            return true;
        }

        public void OnTransitionMapEntryChosen(string entryId)
        {
            var message = new NotifyTransitionMapEntryChosen
            {
                EntryId = entryId
            };
            Send(message);
        }

        public void OnIslandMapEntryChosen(NetworkIslandMapTransition islandMapTransition)
        {
            var message = new NotifyIslandMapEntryChosen
            {
                Island = Mapper.Map<Networking.Messages.Contracts.NetworkIslandMapTransition>(islandMapTransition)
            };
            Send(message);
        }

        public void OnTransitionMapClosed()
        {
            ResetPlayersTracker(Game.PlayersInTransitionMap);
            var message = new NotifyTransitionMapClosed();
            Send(message);
        }

        public void OnGlobalMapTeleport(NetworkGlobalMapLocation globalMapLocation)
        {
            var message = new NotifyGlobalMapTeleport
            {
                Location = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapLocation>(globalMapLocation)
            };
            Send(message);
        }

        protected override bool OnToggleOffPause(out bool showReason)
        {
            showReason = true;
            return TryEndForcedPause(Game.LocalPlayerId);
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

            // .Result is important to block current (main) thread
            return _networkServer.SendAndWaitForAsync<DiceRollValueResponse>(character.Owner.Id, rollRequest).Result;
        }

        protected override void Send(object message)
        {
            Logger.LogObject(LogLevel.Information, "Sending {MessageType}.", message);
            _networkServer.SendAll(message);
        }

        protected override void Send(long playerId, object message)
        {
            Logger.LogObject(LogLevel.Information, "Sending {MessageType} to Player {PlayerId}.", message, playerId);
            _networkServer.Send(playerId, message);
        }

        protected override void OnLocalPlayerTurnStart()
        {
            SetCombatTurnStage(NetworkCombatTurnStage.Starting);
            AddPlayerReadyStatus(PlayerTurnReadinessType.Start, Game.LocalPlayerId, Game.Combat.Turn.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitSynchronization, Game.LocalPlayerId, Game.Combat.Turn.UnitId);

            TryStartTurn();
        }

        protected override void OnLocalPlayerTurnEnd()
        {
            base.OnLocalPlayerTurnEnd();

            Game.Combat.Turn.PlayersEndTurnInitialization.Add(Game.LocalPlayerId);
            Game.Combat.Turn.PlayersEndTurnSynchronization.Add(Game.LocalPlayerId);

            TryEndTurn();
        }

        private void TryStartTurn()
        {
            try
            {
                Logger.LogInformation("Checking if turn could be started. Round={Round}, UnitId={UnitId}", Game.Combat.Round, Game.Combat.Turn?.UnitId);

                lock (ActionLock)
                {
                    if (Game.Combat.Turn == null
                        || (Game.Combat.Turn.Stage != NetworkCombatTurnStage.Starting && Game.Combat.Turn.Stage != NetworkCombatTurnStage.StartSynchronization))
                    {
                        Logger.LogWarning("Turn is not ready to be started yet. TurnStatus={TurnStatus}", Game.Combat.Turn?.Stage);
                        return;
                    }

                    var desyncedPlayers = Game.Combat.PlayersNextTurnInitialization.Where(k => !string.Equals(k.Key, Game.Combat.Turn.UnitId, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (desyncedPlayers.Count > 0)
                    {
                        foreach (var desynced in desyncedPlayers)
                        {
                            Game.Combat.PlayersNextTurnInitialization.TryRemove(desynced.Key, out _);
                        }

                        var players = desyncedPlayers.SelectMany(x => x.Value).Distinct().ToList();
                        Logger.LogWarning("Players have started different turn. Initiating recovering. Players={Players}", desyncedPlayers.ToDictionary(x => x.Key, x => x.Value.ToList()));
                        foreach (var playerId in players)
                        {
                            var player = GetPlayer(playerId);
                            if (player == null)
                            {
                                continue;
                            }

                            PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.Turn.HostOrderDesync.Key, CombatTextSeverity.Debug, player.Name);

                            var desyncedTurnStartMessage = new NotifyInvalidCombatTurnStarted
                            {
                                UnitId = Game.Combat.Turn.UnitId,
                            };
                            Send(playerId, desyncedTurnStartMessage);
                        }

                        return;
                    }

                    var notInitializedPlayers = GetMissingPlayers(Game.Combat.Turn.UnitId, Game.Combat.PlayersNextTurnInitialization);
                    if (notInitializedPlayers.Count > 0)
                    {
                        Logger.LogInformation("Unable to start turn due to missing players turn initialization. MissingPlayersCount={MissingPlayersCount}, MissingPlayers={MissingPlayers}", notInitializedPlayers.Count, string.Join(";", notInitializedPlayers.Select(p => p.Name)));
                        return;
                    }

                    if (Game.Combat.Turn.Stage == NetworkCombatTurnStage.Starting)
                    {
                        SetCombatTurnStage(NetworkCombatTurnStage.StartSynchronization);
                        var combatState = CombatInteraction.GetCombatState();
                        var syncMessage = new NotifyCombatTurnStartSynchronizationRequired
                        {
                            CombatState = Mapper.Map<Networking.Messages.Contracts.NetworkCombatState>(combatState),
                            TriggeredAreaEffects = Mapper.Map<List<Networking.Messages.Contracts.NetworkAreaEffect>>(Game.Combat.TriggeredAreaEffects)
                        };
                        Game.Combat.TriggeredAreaEffects.Clear();
                        Send(syncMessage);
                    }

                    var notSynchronizedPlayers = GetMissingPlayers(Game.Combat.Turn.UnitId, Game.Combat.PlayersNextTurnSynchronization);
                    if (notSynchronizedPlayers.Count > 0)
                    {
                        Logger.LogInformation("Unable to start turn due to missing players turn synchronization. MissingPlayers={MissingPlayers}", string.Join(";", notSynchronizedPlayers.Select(p => p.Name)));
                        return;
                    }

                    Game.Combat.PlayersNextTurnInitialization.Clear();
                    Game.Combat.PlayersNextTurnSynchronization.Clear();

                    DiceRollStorage.Reset();
                    Logger.LogInformation("Dice roll storage has been reset at turn entites sync stage");

                    ValueGenerator.ResetSeededGenerators(IdentifierLifetime.CombatTurn);
                    Game.Combat.Turn.Seed = CreateRandomSeed();

                    var message = new NotifyCombatTurnStarted
                    {
                        Round = Game.Combat.Round,
                        UnitId = Game.Combat.Turn.UnitId,
                        Seed = Game.Combat.Turn.Seed,
                    };

                    Send(message);
                    SetCombatTurnStage(NetworkCombatTurnStage.Playing);
                }

                CombatInteraction.StartTurnBasedCombatTurn(Game.Combat.Turn.UnitId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while trying to start turn");
                throw;
            }
        }

        private void TryEndTurn()
        {
            try
            {
                Logger.LogInformation("Checking if turn could be ended. Round={Round}, UnitId={UnitId}, IsAI={IsAI}", Game.Combat.Round, Game.Combat.Turn.UnitId, Game.Combat.Turn.IsAI);
                var allPlayers = GetSyncedPlayersCount();
                lock (ActionLock)
                {
                    var initializedPlayers = Game.Combat.Turn.PlayersEndTurnInitialization.Count;
                    if (initializedPlayers < allPlayers)
                    {
                        Logger.LogInformation("Can't end turn due to missing player turn end initialization. ReadyPlayers={ReadyPlayers}, RequiredPlayers={RequiredPlayers}", initializedPlayers, allPlayers);
                        return;
                    }

                    if (Game.Combat.Turn.Stage == NetworkCombatTurnStage.Ending)
                    {
                        SetCombatTurnStage(NetworkCombatTurnStage.EndSynchronization);
                        var units = CombatInteraction.GetUnitsInCombat();
                        var turnEndSyncMessage = new NotifyCombatTurnEndSynchronizationRequired
                        {
                            Units = Mapper.Map<List<Networking.Messages.Contracts.NetworkUnit>>(units)
                        };
                        Send(turnEndSyncMessage);
                    }

                    var turnEndSyncedPlayers = Game.Combat.Turn.PlayersEndTurnSynchronization.Count;
                    if (turnEndSyncedPlayers < allPlayers)
                    {
                        Logger.LogInformation("Can't end turn due to missing player turn end synchronization. ReadyPlayers={ReadyPlayers}, RequiredPlayers={RequiredPlayers}", turnEndSyncedPlayers, allPlayers);
                        return;
                    }

                    Logger.LogInformation("Turn has been ended");
                    var turnEndMessage = new NotifyCombatTurnEnded();
                    Send(turnEndMessage);

                    // Game calls 'turn end' every tick, no need for extra calls
                    SetCombatTurnStage(NetworkCombatTurnStage.Ended);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while trying to end turn");
                throw;
            }
        }

        protected override void OnAfterNetworkMessageHandled(long senderPlayerId, object message)
        {
            Logger.LogDebug("Resending message. ExceptPlayerId={ExceptPlayerId}, MessageType={MessageType}", senderPlayerId, message.GetType().Name);
            _networkServer.SendAllExcept(senderPlayerId, message);
        }

        protected override void OnLocalRestGameModeEnded()
        {
            base.OnLocalRestGameModeEnded();

            if (Game.ForcedPause != null)
            {
                Game.ForcedPause.ReadyPlayers.Add(Game.LocalPlayerId);
                TryEndForcedPause(Game.LocalPlayerId);
            }
        }

        protected override void OnRemoteRestGameModeEnded(long playerId)
        {
            base.OnRemoteRestGameModeEnded(playerId);

            if (Game.ForcedPause != null)
            {
                lock (ActionLock)
                {
                    Game.ForcedPause.ReadyPlayers.Add(playerId);
                    TryEndForcedPause(Game.LocalPlayerId);
                }
            }
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

               // pausing
               .On<ClientTogglePauseOff>(OnClientTogglePauseOff)

               // lobby
               .On<NotifyLobbySyncStatusChanged>(OnNotifyLobbySyncStatusChanged)
               .On<ClientGameServerConnectionConfirmed>(OnClientGameServerConnectionConfirmed)

               // area transitioning
               .On<ClientAreaLoaded>(OnClientAreaLoaded)

               // leveling
               .On<ClientCharacterLevelingRequested>(OnClientCharacterLevelingRequested)

               // combat
               .On<ClientCombatPreparationStarted>(OnClientCombatPreparationStarted)
               .On<ClientCombatPreparationCompleted>(OnClientCombatPreparationCompleted)
               .On<ClientCombatInitializationCompleted>(OnClientCombatInitializationCompleted)
               .On<ClientCombatTurnStarted>(OnClientCombatTurnStarted)
               .On<ClientCombatTurnStartSynchronized>(OnClientCombatTurnSynchronized)
               .On<ClientCombatTurnEndSynchronized>(OnClientCombatTurnEndSynchronized)
               .On<NotifyCombatLocalTurnEnded>(OnNotifyCombatLocalTurnEnded)

               // global map & crusade combat
               .On<NotifyGlobalMapTravelerModeChanged>(OnNotifyGlobalMapTravelerModeChanged)
               .On<NotifyTacticalCombatInitializationConfirmed>(OnNotifyTacticalCombatInitializationConfirmed)
               .On<NotifyGlobalMapCrusadeArmyMergeCartClosed>(OnNotifyGlobalMapCrusadeArmyMergeCartClosed)
               .On<NotifyGlobalMapCrusadeArmyInfoShown>(OnNotifyGlobalMapCrusadeArmyInfoShown)
               .On<NotifyGlobalMapCrusadeArmySetLeaderClosed>(OnNotifyGlobalMapCrusadeArmySetLeaderClosed)
               .On<NotifyGlobalMapCrusadeArmyBuyLeaderClosed>(OnNotifyGlobalMapCrusadeArmyBuyLeaderClosed)
               .On<NotifyGlobalMapRecruitmentShown>(OnNotifyGlobalMapRecruitmentShown)
               .On<NotifyGlobalMapRecruitmentClosed>(OnNotifyGlobalMapRecruitmentClosed)
               .On<NotifyGlobalMapCommonPopupShown>(OnNotifyGlobalMapCommonPopupShown)

               // kingdom
               .On<NotifyKingdomNavigationChanged>(OnNotifyKingdomNavigationChanged)

               // dialogs
               .On<ClientDialogCueAnswerSuggested>(OnClientDialogCueAnswerSuggested)
               .On<ClientDialogStartRequested>(OnClientDialogStartRequested)
               .On<ClientDialogCueWitnessed>(OnClientDialogCueWitnessed)

               // pause
               .On<ClientGameAutoPaused>(OnClientGameAutoPaused)

               // inventory
               .On<NotifyPolymorphicItemCreationRequested>(OnNotifyPolymorphicItemCreationRequested)
               ;
        }

        private void OnNotifyKingdomNavigationChanged(long receivedFrom, NotifyKingdomNavigationChanged message)
        {
            var navigation = Mapper.Map<KingdomNavigationType>(message.Type);
            Game.PlayersInKingdomNavigationType.AddOrUpdate(receivedFrom, navigation, (key, existing) => navigation);
        }

        private async void OnNotifyCombatLocalTurnEnded(long receivedFrom, NotifyCombatLocalTurnEnded combatTurnEnded)
        {
            await WaitWhileTrue(CombatInteraction.IsRiderActive, "Waiting for all combat commands to finish before ending turn");

            Game.Combat.Turn.PlayersEndTurnInitialization.Add(combatTurnEnded.PlayerId);
            CombatInteraction.EndTurnBasedCombatTurn();

            TryEndTurn();

            OnAfterNetworkMessageHandled(receivedFrom, combatTurnEnded);
        }

        private void OnNotifyGlobalMapCommonPopupShown(long receivedFrom, NotifyGlobalMapCommonPopupShown globalMapCommonPopupShown)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCommonPopup, globalMapCommonPopupShown.PlayerId);
            var popup = Mapper.Map<NetworkGlobalMapCommonPopup>(globalMapCommonPopupShown.Popup);
            UpdateGlobalMapCommonPopupUIState(popup);

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCommonPopupShown);
        }

        private async void OnClientCombatPreparationStarted(long receivedFrom, ClientCombatPreparationStarted message)
        {
            var units = Mapper.Map<List<NetworkUnit>>(message.Units);

            var isOk = await WaitWhileTrue(() => Game.Combat == null || Game.Combat.Stage != NetworkCombatStage.Idle, $"Waiting for combat to start to add preparation. PlayerId={receivedFrom}",
                TimeSpan.FromSeconds(10));

            if (isOk)
            {
                Game.Combat.PlayersCombatPreparation.TryAdd(receivedFrom, units);
                return;
            }

            lock (ActionLock)
            {
                if (Game.Combat.IsRecovering)
                {
                    Logger.LogInformation("Combat is already in recovering state");
                    return;
                }

                var receivedFromPlayer = GetPlayer(receivedFrom);
                if (receivedFromPlayer == null)
                {
                    Logger.LogWarning("Combat startup desync has been detected, but player is missing. Ignoring...");
                    return;
                }

                InitiateCombatRecovering(receivedFromPlayer);
            }
        }

        private void InitiateCombatRecovering(NetworkPlayer initiator)
        {
            foreach (var player in GetPlayers())
            {
                DiceRollStorage.UndoClaiming(player.Id);
            }

            Logger.LogWarning("Initiating combat recovery");
            Game.Combat.IsRecovering = true;
            Game.Combat.IsPrepared = false;
            Game.Combat.IsPreparationStarted = false;
            Game.Combat.IsInitialized = false;
            Game.Combat.IsPlaying = false;
            Game.Combat.PlayersCombatPreparation.Clear();
            Game.Combat.PlayersCombatInitialization.Clear();
            Game.Combat.StartedAt = DateTime.UtcNow;
            PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.StartupDesync.Host.Key, CombatTextSeverity.Critical, initiator.Name);
            SetCombatStage(NetworkCombatStage.Idle);

            var recoveryMessage = new NotifyCombatRecoveryRequired();
            Send(recoveryMessage);
        }

        private void OnClientCombatPreparationCompleted(long receivedFrom, ClientCombatPreparationCompleted message)
        {
            Game.Combat.PlayersCombatPreparation.TryRemove(message.PlayerId, out _);
            Logger.LogInformation("Combat preparation updated. ConfirmedPlayer={ConfirmedPlayer}, PlayersLeft={PlayersLeft}", message.PlayerId, Game.Combat.PlayersCombatPreparation.Keys);
        }

        private void OnClientTogglePauseOff(long receivedFrom, ClientTogglePauseOff message)
        {
            lock (ActionLock)
            {
                if (Game.ForcedPause != null && (Game.ForcedPause.Reason == NetworkForcedPauseReason.AreaLoading || Game.ForcedPause.IsLifting))
                {
                    Logger.LogWarning("Skipping unpause request for AreaLoading pause");
                    return;
                }
            }

            TryEndForcedPause(message.PlayerId);
        }

        private void OnNotifyGlobalMapRecruitmentClosed(long receivedFrom, NotifyGlobalMapRecruitmentClosed message)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapRecruitment);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapRecruitmentShown(long receivedFrom, NotifyGlobalMapRecruitmentShown message)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapRecruitment, message.PlayerId);
            UpdateGlobalMapRecruitmentUIState();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapCrusadeArmyBuyLeaderClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyBuyLeaderClosed message)
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyBuyLeader, message.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateAfterBuyLeader();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderClosed(long receivedFrom, NotifyGlobalMapCrusadeArmySetLeaderClosed message)
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmySetLeader, message.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateAfterSetLeader();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoShown(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoShown message)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyInfo, message.PlayerId);

            UpdateGlobalMapCrusadeArmyInfoUIState();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapCrusadeArmyMergeCartClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyMergeCartClosed globalMapCrusadeArmyInfoMergeClosed)
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyInfoMerge, globalMapCrusadeArmyInfoMergeClosed.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateAfterMerge();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCrusadeArmyInfoMergeClosed);
        }

        private void OnNotifyTacticalCombatInitializationConfirmed(long receivedFrom, NotifyTacticalCombatInitializationConfirmed tacticalCombatInitializationConfirmed)
        {
            Game.ArmyCombat.PlayersCombatInitialization.TryAdd(tacticalCombatInitializationConfirmed.PlayerId, true);

            Game.ArmyCombat.IsInitialized = TryConfirmTacticalCombatInitialization();
        }

        private void OnNotifyGlobalMapTravelerModeChanged(long receivedFrom, NotifyGlobalMapTravelerModeChanged globalMapTravelerModeChanged)
        {
            var travelerMode = Mapper.Map<NetworkGlobalMapTravelerMode>(globalMapTravelerModeChanged.TravelerMode);
            RegisterGlobalMapMode(globalMapTravelerModeChanged.PlayerId, travelerMode);
            UpdateGlobalMapUIState();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapTravelerModeChanged);
        }

        private void OnNotifyPolymorphicItemCreationRequested(long playerId, NotifyPolymorphicItemCreationRequested polymorphicItemCreationRequested)
        {
            var polymorphicItem = Mapper.Map<NetworkPolymorphicItem>(polymorphicItemCreationRequested.PolymorphicItem);
            GameInteraction.CreateAndEquipPolymorphicItem(polymorphicItem, createContext: false);

            // clients will receive 'NotifyPolymorphicItemCreated' as a result of polymorphic item creation
        }

        private void OnClientGameAutoPaused(long playerId, ClientGameAutoPaused clientGameAutoPaused)
        {
            var pause = Mapper.Map<NetworkForcedPause>(clientGameAutoPaused.Pause);
            lock (ActionLock)
            {
                EnsureForcePaused(pause.Reason, pause.RemovalDelay);
                Game.ForcedPause.ReadyPlayers.Add(playerId);
            }
        }

        private void OnClientCharacterLevelingRequested(long playerId, ClientCharacterLevelingRequested characterLevelingRequested)
        {
            if (!Enum.TryParse<NetworkLevelingType>(characterLevelingRequested.Type, true, out var levelingType))
            {
                Logger.LogError("Invalid char gen screen type value. Value={Value}", characterLevelingRequested.Type);
                return;
            }

            lock (ActionLock)
            {
                if (Game.Leveling == null)
                {
                    InitiateLeveling(characterLevelingRequested.UnitId, levelingType);
                    LevelingInteraction.StartLeveling(characterLevelingRequested.UnitId, levelingType);
                    SendLevelingStartedConfirmation();
                    return;
                }

                // force specific player to open correct leveling ui
                Logger.LogWarning("Leveling is already in progress. PlayerId={PlayerId}, UnitId={UnitId}, LevelingType={LevelingType}, RequestedUnitId={RequestedUnitId}, RequestedLevelingType={RequestedLevelingType}", playerId, Game.Leveling.UnitId, Game.Leveling.Type, characterLevelingRequested.UnitId, characterLevelingRequested.Type);
                SendLevelingStartedConfirmation(playerId);
            }
        }

        private async void OnRandomEncounterContextRequest(long receivedFrom, RandomEncounterContextRequest request)
        {
            var randomEncounterIndex = request.SleepPhase - 1;
            var timeout = Task.Delay(request.Timeout);
            await WaitWhileTrue(() => !timeout.IsCompleted && (Game.Rest == null || Game.Rest.RandomEncounters.Count <= randomEncounterIndex),
                $"Rest Random Encounter is not available yet. RequestedSleepPhase={request.SleepPhase}, RandomEncounterIndex={randomEncounterIndex}");

            var encounter = timeout.IsCompleted ? null : Game.Rest?.RandomEncounters[randomEncounterIndex];
            var response = new RandomEncounterContextResponse
            {
                Encounter = Mapper.Map<Networking.Messages.Contracts.NetworkRandomEncounter>(encounter)
            };
            Send(receivedFrom, response);
        }

        private void OnClientCombatTurnEndSynchronized(long playerId, ClientCombatTurnEndSynchronized message)
        {
            Game.Combat.Turn.PlayersEndTurnSynchronization.Add(playerId);
            TryEndTurn();
        }

        private void OnClientCombatTurnSynchronized(long playerId, ClientCombatTurnStartSynchronized synchronized)
        {
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitSynchronization, playerId, synchronized.UnitId);
            TryStartTurn();
        }

        private void OnClientCombatTurnStarted(long playerId, ClientCombatTurnStarted started)
        {
            AddPlayerReadyStatus(PlayerTurnReadinessType.Start, playerId, started.UnitId);

            TryStartTurn();
        }

        private void OnClientCombatInitializationCompleted(long playerId, ClientCombatInitializationCompleted message)
        {
            if (!Game.Combat.PlayersCombatInitialization.TryAdd(playerId, true))
            {
                Logger.LogWarning("Received duplicate client initialization. PlayerId={PlayerId}", playerId);
            }
        }

        private async void OnClientDialogStartRequested(long playerId, ClientDialogStartRequested requested)
        {
            var dialog = Mapper.Map<NetworkDialog>(requested.Dialog);
            var hasBeenStarted = await DialogInteraction.StartDialogAsync(dialog);
            if (hasBeenStarted)
            {
                return;
            }

            Logger.LogInformation("Host dialog is already in progress. Sending dialog confirmation");
            var message = new NotifyDialogStarted
            {
                Dialog = Mapper.Map<Networking.Messages.Contracts.NetworkDialog>(Game.DialogState.Dialog)
            };
            Send(message);
        }

        private async void OnClientDialogCueAnswerSuggested(long playerId, ClientDialogCueAnswerSuggested message)
        {
            await WaitWhileTrue(() => Game.DialogState == null, "Waiting for dialog to initialize before suggesting cue answer");

            Game.DialogState.AnswerSuggestions.AddOrUpdate(playerId, message.AnswerName, (key, existing) =>
            {
                return message.AnswerName;
            });

            List<NetworkDialogAnswerSuggestion> suggestions = [.. Game.DialogState.AnswerSuggestions.GroupBy(x => x.Value, x => x.Key).Select(x => new NetworkDialogAnswerSuggestion { AnswerName = x.Key, Players = [.. x] })];
            DialogInteraction.MarkSuggestedDialogAnswers(suggestions);

            var cueAnswerSuggested = new NotifyDialogCueAnswerSuggested
            {
                Dialog = Mapper.Map<Networking.Messages.Contracts.NetworkDialog>(Game.DialogState.Dialog),
                CueName = message.CueName,
                Suggestions = Mapper.Map<List<Networking.Messages.Contracts.NetworkDialogAnswerSuggestion>>(suggestions),
            };
            Send(cueAnswerSuggested);
        }

        private async void OnClientDialogCueWitnessed(long playerId, ClientDialogCueWitnessed message)
        {
            await WaitWhileTrue(() => Game.DialogState == null, "Waiting for dialog to initialize before witnessing cue");

            AddCueWitness(message.CueName, playerId);
            TryEnableDialogContinueButton();
        }

        private async void OnDiceRollValueRequest(long playerId, DiceRollValueRequest request)
        {
            try
            {
                // roll storage (location) is dynamic in combat and depends on TurnOwner
                var (shouldBeProxied, playerToAsk) = ShouldRollBeProxied(playerId, request);
                if (!shouldBeProxied)
                {
                    await SendLocalRollAsync(playerId, request);
                    return;
                }

                var message = new DiceRollValueRequest
                {
                    RollId = request.RollId,
                    UnitId = request.UnitId,
                    Timeout = request.Timeout,
                    PlayerId = playerId
                };
                Logger.LogInformation("Asking another client for a roll. PlayerToAsk={PlayerToAsk}, PlayerId={PlayerId}, RollId={RollId}, UnitId={UnitId}, Timeout={Timeout}", playerToAsk, message.PlayerId, message.RollId, message.UnitId, message.Timeout);
                var rollFromAnotherClient = await _networkServer.SendAndWaitForAsync<DiceRollValueResponse>(playerToAsk.Value, message);
                Send(playerId, rollFromAnotherClient);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to process dice roll value request. PlayerId={PlayerId}, RollId={RollId}, UnitId={UnitId}", playerId, request.RollId, request.UnitId);
                throw;
            }
        }

        private (bool, long?) ShouldRollBeProxied(long playerId, DiceRollValueRequest diceRollValueRequest)
        {
            // either combat has already ended on the client (last turn action) or it's a mid combat unit join which rolls initiative
            if (diceRollValueRequest.CombatTurnUnitId == null || string.Equals(diceRollValueRequest.RuleName, "RuleInitiativeRoll", StringComparison.OrdinalIgnoreCase))
            {
                return (false, null);
            }

            var turn = SelectValidTurn(diceRollValueRequest.CombatTurnUnitId);
            var characterTurn = GetPartyCharacter(turn?.UnitId);
            var shouldRollBeProxied = (Game.Combat == null || Game.Combat.IsInitialized) && turn != null && !turn.IsLocalPlayer && !turn.IsAI && characterTurn != null && characterTurn.Owner.Id != playerId && characterTurn.Owner.Id != Game.LocalPlayerId;
            return (shouldRollBeProxied, characterTurn?.Owner?.Id);
        }

        private NetworkCombatTurn SelectValidTurn(string unitId)
        {
            // LastCombatTurn is valid only when:
            // - combat is already ended locally
            // - AI turn is already ended locally
            return CheckTurnValidity(Game.Combat?.Turn, unitId) ?? CheckTurnValidity(Game.LastCombatTurn, unitId);
        }

        private NetworkCombatTurn CheckTurnValidity(NetworkCombatTurn turn, string unitId)
        {
            if (turn == null || !string.Equals(turn.UnitId, unitId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return turn;
        }

        private void OnNotifyLobbySyncStatusChanged(long receivedFrom, NotifyLobbySyncStatusChanged lobbySyncStatusChanged)
        {
            var status = Mapper.Map<NetworkLobbySyncStatus>(lobbySyncStatusChanged.Status);
            UpdateLobbySyncStatus(lobbySyncStatusChanged.PlayerId, status);

            TryStartSavedGame();

            OnAfterNetworkMessageHandled(receivedFrom, lobbySyncStatusChanged);
        }

        private async void OnClientAreaLoaded(long receivedFrom, ClientAreaLoaded message)
        {
            await WaitWhileTrue(() => Game.ForcedPause == null, "Waiting for game to finish loading and initialize local force pause");
            lock (ActionLock)
            {
                Game.ForcedPause.ReadyPlayers.Add(receivedFrom);
            }

            TryEndForcedPause(receivedFrom);
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
                if (settings != null)
                {
                    settings.Multiplayer = SettingsService.GetSettings();
                }

                var message = new GameServerConnectionSucceeded
                {
                    ClientPlayerId = playerId,
                    GameSettings = Mapper.Map<Networking.Messages.Contracts.NetworkGameSettings>(settings),
                    SessionSeed = Game.SessionSeed
                };
                Send(playerId, message);
            }
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
                Logger.LogObject(LogLevel.Information, "Sending {MessageType} to all EXCEPT Player {PlayerId}.", playersChanged, playerId);
                _networkServer.SendAllExcept(playerId, playersChanged);
                ShowPlayerDisconnectedMessage(removedPlayer);

                TryEnableDialogContinueButton();

                RefreshUIOnPlayerDisconnect(removedPlayer.Id);

                TryEndForcedPause(Game.LocalPlayerId);
            }
        }

        private void OnClientGameServerConnectionConfirmed(long playerId, ClientGameServerConnectionConfirmed connectionConfirmed)
        {
            try
            {
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
                    Send(playersChanged);

                    var lobbyCharactersChanged = new NotifyLobbyCharactersChanged
                    {
                        Title = Game.StartUp?.Title,
                        Characters = Mapper.Map<List<Networking.Messages.Contracts.NetworkCharacter>>(Game.Characters)
                    };
                    Send(playerId, lobbyCharactersChanged);

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
                    discrepantMods.Add(new NetworkDiscrepantMod(hostMod.Id, hostMod.Type, clientMod?.Version, hostMod.Version, reason.Value));
                }

                if (clientMod != null)
                {
                    clientMods.Remove(clientMod);
                }
            }

            var enabledLeftovers = clientMods.Where(x => x.IsEnabled).Select(x => new NetworkDiscrepantMod(x.Id, x.Type, x.Version, null, NetworkDiscrepancyReason.Extra)).ToList();
            discrepantMods.AddRange(enabledLeftovers);

            return discrepantMods;
        }

        private NotifyLobbyPlayersChanged CreateNotifyLobbyPlayersChanged()
        {
            var playersChanged = new NotifyLobbyPlayersChanged
            {
                Players = Mapper.Map<List<Networking.Messages.Contracts.NetworkPlayer>>(GetPlayers())
            };
            return playersChanged;
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
                    QuickMovement = false
                },
                Autopause = new NetworkAutopauseSettings
                {
                    PauseOnTrapDetected = true,
                    PauseOnSpellcastInterrupted = Kingmaker.Settings.EntitiesType.None,
                    PauseOnSpellcastStarted = Kingmaker.Settings.EntitiesType.None,
                    // everything else is false for autopause
                },
                // tutorial is disabled because most of tutorial popups pause the game
                Tutorial = new NetworkTutorialSettings()
            };

            return settings;
        }

        private void AddCueWitness(string cueName, long playerId)
        {
            if (Game.DialogState == null)
            {
                Logger.LogError("Trying to add witness to null dialog. CueName={CueName}, PlayerId={PlayerId}", cueName, playerId);
                return;
            }

            Game.DialogState.CueViews.AddOrUpdate(cueName, (key) => new HashSet<long>([playerId]), (key, existing) =>
            {
                existing.Add(playerId);
                return existing;
            });

            Logger.LogInformation("Cue witness has been added. CueName={CueName}, PlayerId={PlayerId}", cueName, playerId);
        }

        private bool TryConfirmTacticalCombatInitialization()
        {
            if (Game.ArmyCombat.IsInitialized)
            {
                return false;
            }

            var everyoneIsReady = Game.ArmyCombat.PlayersCombatInitialization.Count(x => x.Value) >= GetSyncedPlayersCount();
            Logger.LogInformation("Checking crusade army combat initialziation. IsReady={IsReady}", everyoneIsReady);
            return everyoneIsReady;
        }

        private List<NetworkPlayer> GetPlayersWhoHaveNotSeenCueYet(string cueName)
        {
            if (Game.DialogState == null)
            {
                Logger.LogWarning("Trying to get cue players, but dialog is null. CueName={CueName}", cueName);
                return [];
            }

            if (!Game.DialogState.CueViews.TryGetValue(cueName ?? string.Empty, out var cueViews))
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
            if (Game.DialogState == null)
            {
                Logger.LogWarning("Unable to enable continue button because current dialog is null");
                return;
            }

            var currentCue = Game.DialogState.CurrentCueName;
            var missingPlayers = GetPlayersWhoHaveNotSeenCueYet(currentCue);
            if (missingPlayers.Count > 0)
            {
                Logger.LogInformation("Cannot proceed with dialog yet. CurrentCue={CurrentCue}, MissingPlayers={MissingPlayers}", currentCue, string.Join(";", missingPlayers.Select(x => x.Name)));
                return;
            }

            Logger.LogInformation("All players have witnessed current cue. CueName={CueName}", currentCue);
            DialogInteraction.SetDialogContinueButtonState(true);
        }

        private List<NetworkPlayer> GetPlayersNotReadyToUnpause(long requestedByPlayerId)
        {
            var result = new List<NetworkPlayer>();
            var players = GetSyncedPlayers();
            foreach (var player in players)
            {
                if (player.Id == requestedByPlayerId
                    || Game.ForcedPause.ReadyPlayers.Contains(player.Id)
                    || Game.ForcedPause.Reason != NetworkForcedPauseReason.AreaLoading && player.Id == Game.LocalPlayerId && !GameInteraction.IsPaused)
                {
                    continue;
                }

                result.Add(player);
            }

            return result;
        }

        private bool TryEndForcedPause(long requestedByPlayerId)
        {
            try
            {
                Logger.LogInformation("Checking if forced pause could be removed. PauseIsNull={PauseIsNull}, IsLifting={IsLifting}, RequestedByPlayer={RequestedByPlayer}", Game.ForcedPause == null, Game.ForcedPause?.IsLifting, requestedByPlayerId);

                if (Game.ForcedPause == null)
                {
                    return true;
                }

                if (Game.ForcedPause.IsLifting)
                {
                    return false;
                }

                lock (ActionLock)
                {
                    var missingPlayer = GetPlayersNotReadyToUnpause(requestedByPlayerId);
                    if (missingPlayer.Any())
                    {
                        Logger.LogInformation("Not everyone is ready, forced pause will remain. MissingPlayers={MissingPlayers}", missingPlayer.Select(x => x.Name));
                        return false;
                    }

                    var removalDelay = Game.ForcedPause.RemovalDelay;
                    var delay = removalDelay.HasValue ? Task.Delay(removalDelay.Value) : Task.CompletedTask;
                    Game.ForcedPause.IsLifting = true;
                    Logger.LogInformation("Forced pause will be lifted soon. Reason={Reason}, RemovalDelay={RemovalDelay}", Game.ForcedPause.Reason, removalDelay.GetValueOrDefault());
                    delay.ContinueWith(x =>
                    {
                        try
                        {
                            if (Game.ForcedPause == null)
                            {
                                Logger.LogWarning("Previous forced pause lifter has been skipped due to null forcedpause. Most likely game was quickloaded");
                                return;
                            }

                            var message = new NotifyGamePauseEnded
                            {
                                AreaSeed = Game.ForcedPause.Reason == NetworkForcedPauseReason.AreaLoading ? Game.CurrentArea.Seed : null,
                            };

                            if (GameInteraction.IsPaused && Game.ForcedPause.Reason == NetworkForcedPauseReason.AreaLoading)
                            {
                                var party = CombatInteraction.GetParty();
                                message.Party = Mapper.Map<List<Networking.Messages.Contracts.NetworkUnit>>(party);
                            }
                            Send(message);

                            Game.ForcedPause = null;
                            GameInteraction.SetPause(false);
                            Logger.LogInformation("Forced pause has been lifted");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error while unpausing after delay");
                            throw;
                        }
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

        private void TryStartSavedGame()
        {
            lock (ActionLock)
            {
                var canStart = Game.Stage == NetworkLobbyStage.PreparingToStart && Game.Players.All(p => p.LobbySyncStatus == NetworkLobbySyncStatus.Succeed);

                if (canStart)
                {
                    Logger.LogInformation("Everyone is synced, game can be started. LobbyStage={LobbyStage}, IsNewGameSequence={IsNewGameSequence}", Game.Stage, Game.StartUp.IsNewGameSequence);
                    Send(new NotifyGameStarted());

                    if (Game.StartUp.IsNewGameSequence)
                    {
                        StartNewGameSequence();
                        return;
                    }

                    LoadSavedGame();
                }
            }
        }

        private void SendLevelingStartedConfirmation(long? playerId = null)
        {
            var message = new NotifyLevelingStarted
            {
                UnitId = Game.Leveling.UnitId,
                Type = Game.Leveling.Type.ToString()
            };
            if (playerId.HasValue)
            {
                Send(playerId.Value, message);
                return;
            }

            Send(message);
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

        private NetworkCombatUnitDiscrepancy GetDiscrepantCombatUnits()
        {
            var discrepancy = new NetworkCombatUnitDiscrepancy();

            foreach (var (playerId, units) in Game.Combat.PlayersCombatPreparation)
            {
                var others = Game.Combat.PlayersCombatPreparation
                    .Where(kvp => kvp.Key != playerId)
                    .SelectMany(kvp => kvp.Value)
                    .ToHashSet();

                discrepancy.Units[playerId] = [.. units
                    .Where(v => !others.Contains(v))
                    .Distinct()];
            }

            discrepancy.Units = discrepancy.Units.Where(x => x.Value.Count > 0).ToDictionary(x => x.Key, x => x.Value);
            return discrepancy;
        }
    }
}
