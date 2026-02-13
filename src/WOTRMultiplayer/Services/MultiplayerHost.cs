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
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Entities.Units;
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
            Logger.LogInformation("Sending {MessageType} to ALL players. Portraits={Portraits}", nameof(NotifyLobbyCharactersChanged), charactersChanged.Characters.Select(x => x.Portrait));
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

            Logger.LogInformation("Sending {MessageType}. GameId={GameId}, ContentSize={ContentSize}, LoadedSaveSeed={LoadedSaveSeed}", nameof(NotifyLobbySaveGameChanged), saveGameChanged.GameId, saveGameChanged.Content?.Length, saveGameChanged.Seed);
            Send(saveGameChanged);

            TryStartSavedGame();
            return true;
        }

        public override void OnAreaLoadingComplete()
        {
            base.OnAreaLoadingComplete();

            var areaSeed = CreateRandomSeed();
            SetAreaSeed(areaSeed);

            TryEndForcedPause();
        }

        public void OnAreaTransition(NetworkAreaTransition areaTransition)
        {
            var message = new NotifyPartyAreaTransitioned
            {
                Transition = Mapper.Map<Networking.Messages.Contracts.NetworkAreaTransition>(areaTransition)
            };
            Logger.LogInformation("Sending {MessageType}. AreaExitId={AreaExitId}, IsActionsTransition={IsActionsTransition}, FromAreaId={FromAreaId}, FromAreaName={FromAreaName}, ToAreaId={ToAreaId}, ToAreaName={ToAreaName}", nameof(NotifyPartyAreaTransitioned), message.Transition.AreaExitId, message.Transition.IsActionsTransition, message.Transition.From.Id, message.Transition.From.Name, message.Transition.To.Id, message.Transition.To.Name);
            Send(message);
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
                DialogInteraction.PlayUnableToSelectCueAnimation(answerName);
                PlayerNotification.ShowWarningNotification(WellKnownKeys.GameNotifications.Dialogs.WaitingForOtherPlayers.Key, addToLog: false);
                return false;
            }

            DialogInteraction.ResetSuggestedDialogAnswers();
            Game.Dialog.AnswerSuggestions.Clear();
            Game.Dialog.CueViews.TryRemove(cueName, out _);
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

            Send(message);
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
                    return false;
                case NetworkCombatStage.Preparing:
                    if (!Game.Combat.IsPrepared)
                    {
                        var discrepantUnits = GetDiscrepantCombatUnits();
                        var preparationRequiredMessage = new NotifyCombatPreparationRequired
                        {
                            Discrepancy = Mapper.Map<Networking.Messages.Contracts.NetworkCombatUnitDiscrepancy>(discrepantUnits),
                        };
                        Logger.LogInformation("Sending {MessageType}. DiscrepantUnits={DiscrepantUnits}", nameof(NotifyCombatPreparationRequired), preparationRequiredMessage.Discrepancy.Units);
                        Send(preparationRequiredMessage);
                        Game.Combat.IsPrepared = true;
                        _ = FixCombatUnitDiscrepancyAsync(discrepantUnits);
                    }

                    var isPrepared = Game.Combat.PlayersCombatPreparation.Count == 0;
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
                        Logger.LogInformation("Sending {MessageType}. CombatSeed={CombatSeed}, RoundNumber={RoundNumber}, HasSurprisingRound={HasSurprisingRound}, UnitsInCombat={UnitsInCombat}, TriggeredAreaEffects={TriggeredAreaEffects}",
                            nameof(NotifyCombatInitializationRequired), message.CombatSeed, message.State.RoundNumber, message.State.HasSurpriseRound, message.State.Units.Count);
                        Send(message);

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
                        Logger.LogInformation("Sending {MessageType}", nameof(NotifyCombatInitializationCompleted));

                        Game.Combat.IsPlaying = true;
                    }
                    return true;
                default:
                    return Game.Combat.IsPlaying;
            }
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
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, MapObjectId={MapObjectId}, Roll={Roll}", nameof(NotifyPerceptionCheckRolled), message.Check.UnitId, message.Check.MapObject.Id, check.Roll);

            Send(message);
        }

        public void OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check)
        {
            var message = new NotifyInspectionKnowledgeCheckRolled
            {
                Check = Mapper.Map<Networking.Messages.Contracts.NetworkInspectionKnowledgeCheck>(check)
            };
            Logger.LogInformation("Sending {MessageType}. TargetUnitId={TargetUnitId}, InitiatorUnitId={InitiatorUnitId}, StatType={StatType}, DC={DC}, RollResult={RollResult}",
                nameof(NotifyInspectionKnowledgeCheckRolled), message.Check.TargetUnitId, message.Check.InitiatorUnitId, message.Check.StatType, message.Check.DC, message.Check.RollResult);

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
                    EnsureForcePaused(NetworkForcedPauseReason.RestEncounterLoading, settings.ForcedPauseRandomEncounterTerminationDelay);
                    CombatInteraction.UpdateIsInCombatStatus();
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
                var aiActions = GetAIActions();
                if (aiActions == null)
                {
                    return null;
                }

                if (aiActions.Count > 0
                    && aiActions[aiActions.Count - 1].ActionBlueprintId == null
                    && aiActions[aiActions.Count - 1].UnitId == action.UnitId
                    && action.ActionBlueprintId == null)
                {
                    Logger.LogInformation("Duplicate AI action has been skipped. UnitId={UnitId}", action.UnitId);
                }

                aiActions.Add(action);
                Logger.LogInformation("AI action selection has been stored. UnitId={UnitId}, ActionBlueprintId={ActionBlueprintId}, TargetId={TargetId}, Index={Index}", action.UnitId, action.ActionBlueprintId, action.TargetId, aiActions.Count);
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to store ai actions");
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

        public void OnGlobalMapRestOpened()
        {
            var message = new NotifyGlobalMapRestOpened();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapRestOpened));
            Send(message);
        }

        public void OnRestWindowClosed()
        {
            var message = new NotifyRestWindowClosed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyRestWindowClosed));
            Send(message);
        }

        public void OnGlobalMapGroupChangerOpened()
        {
            var message = new NotifyGlobalMapGroupChangerOpened();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapGroupChangerOpened));
            Send(message);
        }

        public void OnGlobalMapTravelStarted(NetworkGlobalMapTravel globalMapTravel)
        {
            var message = new NotifyGlobalMapTravelStarted
            {
                Travel = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapTravel>(globalMapTravel)
            };
            Logger.LogInformation("Sending {MessageType}. Type={Type}, MovementPoints={MovementPoints}, FromClick={FromClick}, DestinationId={DestinationId}, DestinationName={DestinationName}", nameof(NotifyGlobalMapTravelStarted), message.Travel.Type, message.Travel.Traveler.MovementPoints, message.Travel.FromClick, message.Travel.Destination.Id, message.Travel.Destination.Name);
            Send(message);
        }

        public void OnGlobalMapSelectedArmyChanged(NetworkGlobalMapArmy globalMapArmy)
        {
            var message = new NotifyGlobalMapSelectedArmyChanged
            {
                Army = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmy>(globalMapArmy)
            };
            Logger.LogInformation("Sending {MessageType}. ArmyId={ArmyId}", nameof(NotifyGlobalMapSelectedArmyChanged), message.Army?.Id);
            Send(message);
        }

        public void OnGlobalMapAutoCrusadeCombatChanged(bool isEnabled)
        {
            var message = new NotifyGlobalMapAutoCrusadeCombatChanged
            {
                IsEnabled = isEnabled
            };
            Logger.LogInformation("Sending {MessageType}. IsEnabled={IsEnabled}", nameof(NotifyGlobalMapAutoCrusadeCombatChanged), message.IsEnabled);
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
            var readyPlayers = GetPlayersCountWithSyncedGlobalMapMode();
            if (!readyPlayers.HasValue)
            {
                Logger.LogError("Global Map location select has been denied due to invalid ready players value");
                return false;
            }

            var canSelect = Game.ForcedPause == null && readyPlayers.Value >= GetSyncedPlayersCount();
            if (!canSelect)
            {
                PlayerNotification.ShowWarningNotification(WellKnownKeys.GameNotifications.ForcedPause.AreaLoading.Key);
            }

            return canSelect;
        }

        public void OnGlobalMapContinueTravel(NetworkGlobalMapTraveler globalMapTraveler)
        {
            var message = new NotifyGlobalMapTravelContinued
            {
                Traveler = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapTraveler>(globalMapTraveler)
            };
            Logger.LogInformation("Sending {MessageType}. EdgePosition={EdgePosition}", nameof(NotifyGlobalMapTravelContinued), message.Traveler.Position?.EdgePosition);
            Send(message);
        }

        public void OnGlobalMapStopTravel(NetworkGlobalMapTraveler globalMapTraveler)
        {
            var message = new NotifyGlobalMapTravelStopped
            {
                Traveler = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapTraveler>(globalMapTraveler)
            };
            Logger.LogInformation("Sending {MessageType}. EdgePosition={EdgePosition}", nameof(NotifyGlobalMapTravelStopped), message.Traveler.Position?.EdgePosition);
            Send(message);
        }

        public void OnGlobalMapCommonPopupAccepted(NetworkGlobalMapCommonPopup globalMapCommonPopup)
        {
            var message = new NotifyGlobalMapCommonPopupAccepted
            {
                Popup = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapCommonPopup>(globalMapCommonPopup)
            };
            Logger.LogInformation("Sending {MessageType}. Type={Type}, LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapCommonPopupAccepted), message.Popup.Type, message.Popup.Location?.Id, message.Popup.Location?.Name);
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

        public void OnGlobalMapSkipDay()
        {
            var message = new NotifyGlobalMapDaySkipped();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapDaySkipped));
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

        public void OnZoneLootCompleted()
        {
            var message = new NotifyZoneLootCompleted();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyZoneLootCompleted));
            Send(message);
        }

        public void OnZoneLootLeft()
        {
            var message = new NotifyZoneLootLeft();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyZoneLootLeft));
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
            var isInParty = IsControlledByPlayers(polymorphicItem.UnitId) || GameInteraction.IsUnitInParty(polymorphicItem.UnitId);
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

        public void OnCrusadeArmyBattleResultsClosed()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyBattleResults);
            var message = new NotifyCrusadeArmyBattleResultsClosed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyCrusadeArmyBattleResultsClosed));
            Send(message);
        }

        public void OnCrusadeArmyBattleResultsManualCombatStarted()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyBattleResults);
            var message = new NotifyCrusadeArmyBattleResultsManualCombatStarted();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyCrusadeArmyBattleResultsManualCombatStarted));
            Send(message);
        }

        public void OnGlobalMapLocationMessageClosed()
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapLocationMessage, Game.LocalPlayerId);

            var message = new NotifyGlobalMapLocationMessageClosed
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapLocationMessageClosed), message.PlayerId);
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
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, Type={Type}, LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapCommonPopupDeclined), message.PlayerId, message.Popup.Type, message.Popup.Location?.Id, message.Popup.Location?.Name);
            Send(message);
        }

        public void OnGlobalMapCombatResultsClosed()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCombatResults);
            var message = new NotifyGlobalMapCombatResultsClosed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCombatResultsClosed));
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
            Logger.LogInformation("Sending {MessageType}. AreaSeed={AreaSeed}, Seed={Seed}", nameof(NotifyTacticalCombatInitialized), message.AreaSeed, message.Seed);
            Send(message);
        }

        public void OnTacticalCombatUnitUseAbilityCommand(NetworkTacticalUnitUseAbilityCommand tacticalUnitUseAbilityCommand)
        {
            var message = new NotifyTacticalUnitUseAbilityCommandExecuted
            {
                Command = Mapper.Map<Networking.Messages.Contracts.NetworkTacticalUnitUseAbilityCommand>(tacticalUnitUseAbilityCommand)
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, AbilityId={AbilityId}, Path={Path}", nameof(NotifyTacticalUnitUseAbilityCommandExecuted), message.Command.InitiatorUnitId, message.Command.Ability.Id, message.Command.VectorPath);
            Send(message);
        }

        public void OnTacticalCombatUnitAttackCommand(NetworkTacticalUnitAttackCommand tacticalUnitAttackCommand)
        {
            var message = new NotifyTacticalUnitAttackCommandExecuted
            {
                Command = Mapper.Map<Networking.Messages.Contracts.NetworkTacticalUnitAttackCommand>(tacticalUnitAttackCommand)
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, TargetId={TargetId}, Path={Path}", nameof(NotifyTacticalUnitAttackCommandExecuted), message.Command.UnitId, message.Command.TargetUnitId, message.Command.Path);
            Send(message);
        }

        public void OnTacticalCombatUnitMoveToCommand(NetworkTacticalUnitMoveToCommand tacticalUnitMoveToCommand)
        {
            var message = new NotifyTacticalUnitMoveToCommandExecuted
            {
                Command = Mapper.Map<Networking.Messages.Contracts.NetworkTacticalUnitMoveToCommand>(tacticalUnitMoveToCommand)
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, Path={Path}", nameof(NotifyTacticalUnitMoveToCommandExecuted), message.Command.UnitId, message.Command.Path);
            Send(message);
        }

        public bool OnTacticalCombatTotalDefenseUsed()
        {
            var message = new NotifyTacticalCombatTotalDefenseUsed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyTacticalCombatTotalDefenseUsed));
            Send(message);
            return true;
        }

        public bool OnTacticalCombatTurnPostponed()
        {
            var message = new NotifyTacticalCombatTurnPostponed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyTacticalCombatTurnPostponed));
            Send(message);
            return true;
        }

        public void OnTacticalCombatRetreat()
        {
            var message = new NotifyTacticalCombatRetreated();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyTacticalCombatRetreated));
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyInfoClosed()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyInfo);

            var message = new NotifyGlobalMapCrusadeArmyInfoClosed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmyInfoClosed));
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyMoveSquadsToMainArmy()
        {
            var message = new NotifyGlobalMapCrusadeArmySquadsMovedToMainArmy();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmySquadsMovedToMainArmy));
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyMoveSquadsToSecondArmy()
        {
            var message = new NotifyGlobalMapCrusadeArmySquadsMovedToSecondArmy();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmySquadsMovedToSecondArmy));
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyInfoNextMergeArmy()
        {
            var message = new NotifyGlobalMapCrusadeArmyInfoNextMergeArmySelected();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmyInfoNextMergeArmySelected));
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyInfoPrevMergeArmy()
        {
            var message = new NotifyGlobalMapCrusadeArmyInfoPrevMergeArmySelected();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmyInfoPrevMergeArmySelected));
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyLeaderAction(NetworkGlobalMapArmyLeader globalMapArmyLeader, NetworkGlobalMapArmyLeaderActionType armyLeaderActionType)
        {
            var message = new NotifyGlobalMapCrusadeArmyLeaderActionExecuted()
            {
                Type = armyLeaderActionType.ToString(),
                Leader = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmyLeader>(globalMapArmyLeader)
            };
            Logger.LogInformation("Sending {MessageType}. LeaderId={LeaderId}, BlueprintId={BlueprintId}, Type={Type}", nameof(NotifyGlobalMapCrusadeArmyLeaderActionExecuted), message.Leader?.Id, message.Leader?.BlueprintId, message.Type);
            Send(message);
        }

        public void OnGlobalMapMergeArmies()
        {
            var message = new NotifyGlobalMapCrusadeArmiesMerging();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmiesMerging));
            Send(message);
        }

        public void OnGlobalMapCreateCrusadeArmy()
        {
            var message = new NotifyGlobalMapCrusadeArmyCreated();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmyCreated));
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyMainCartClosed()
        {
            var message = new NotifyGlobalMapCrusadeArmyMainCartClosed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmyMainCartClosed));
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyRecruitCartClosed()
        {
            var message = new NotifyGlobalMapCrusadeArmyRecruitCartClosed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmyRecruitCartClosed));
            Send(message);
        }

        public void OnGlobalMapRecruitmentBuyUnits(NetworkGlobalMapUnitRecruitmentOrder globalMapUnitRecruitmentOrder)
        {
            var message = new NotifyGlobalMapUnitsRecruited
            {
                Order = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapUnitRecruitmentOrder>(globalMapUnitRecruitmentOrder)
            };
            Logger.LogInformation("Sending {MessageType}. UnitBlueprintId={UnitBlueprintId}, Count={Count}, ArmyId={ArmyId}, Type={Type}", nameof(NotifyGlobalMapUnitsRecruited), message.Order.BlueprintId, message.Order.Count, message.Order.ArmyId, message.Order.Type);
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyDismiss(NetworkGlobalMapArmy globalMapArmy)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCommonPopup);

            var message = new NotifyGlobalMapCrusadeArmyDismissed
            {
                Army = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmy>(globalMapArmy),
            };
            Logger.LogInformation("Sending {MessageType}. ArmyId={ArmyId}", nameof(NotifyGlobalMapCrusadeArmyDismissed), message.Army.Id);
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyLeaderLevelingClosed()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling);
            var message = new NotifyGlobalMapCrusadeArmyLeaderLevelingClosed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmyLeaderLevelingClosed));
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyLeaderLevelingConfirmed()
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling);
            var message = new NotifyGlobalMapCrusadeArmyLeaderLevelingConfirmed();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmyLeaderLevelingConfirmed));
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyLeaderLevelingSkillSelected(string skillId)
        {
            var message = new NotifyGlobalMapCrusadeArmyLeaderLevelingSkillSelected
            {
                Id = skillId
            };
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmyLeaderLevelingSkillSelected));
            Send(message);
        }

        public void OnGlobalMapMagicSpellUsed(NetworkGlobalMapMagicSpell globalMagicSpell)
        {
            var message = new NotifyGlobalMapMagicSpellUsed
            {
                Spell = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapMagicSpell>(globalMagicSpell),
            };
            Logger.LogInformation("Sending {MessageType}. SpellId={SpellId}, SpellName={SpellName}, TargetArmies={TargetArmies}, LocationId={LocationId}", nameof(NotifyGlobalMapMagicSpellUsed), message.Spell.Id, message.Spell.Name, message.Spell.TargetArmies, message.Spell.Location?.Id);
            Send(message);
        }

        public void OnGlobalMapRecruitmentBuyResources(NetworkGlobalMapResourceOrder globalMapResourceOrder)
        {
            var message = new NotifyGlobalMapResourcesBought
            {
                Order = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapResourceOrder>(globalMapResourceOrder)
            };
            Logger.LogInformation("Sending {MessageType}. FinalCost={FinalCost}, FinanceCount={FinanceCount}, MaterialsCount={MaterialsCount}", nameof(NotifyGlobalMapResourcesBought), message.Order.FinalCost, message.Order.FinanceCount, message.Order.MaterialCount);
            Send(message);
        }

        public void OnGlobalMapCrusadeArmyCartNameChanged(NetworkGlobalMapArmy globalMapArmy)
        {
            var message = new NotifyGlobalMapCrusadeArmyInfoCartNameChanged
            {
                Army = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmy>(globalMapArmy)
            };
            Logger.LogInformation("Sending {MessageType}. ArmyId={ArmyId}, Name={Name}", nameof(NotifyGlobalMapCrusadeArmyInfoCartNameChanged), message.Army.Id, message.Army.Name);
            Send(message);
        }

        public void OnGlobalMapCrusadeArmySetLeaderClear()
        {
            var message = new NotifyGlobalMapCrusadeArmySetLeaderClearClicked();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmySetLeaderClearClicked));
            Send(message);
        }

        public void OnGlobalMapCrusadeArmySetLeaderRecruit()
        {
            var message = new NotifyGlobalMapCrusadeArmySetLeaderRecruitClicked();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapCrusadeArmySetLeaderRecruitClicked));
            Send(message);
        }

        public void OnGlobalMapRecruitmentMercReroll()
        {
            var message = new NotifyGlobalMapRecruitmentMercenariesRerolled();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapRecruitmentMercenariesRerolled));
            Send(message);
        }

        public void OnGlobalMapRecruitmentNextArmy()
        {
            var message = new NotifyGlobalMapRecruitmentNextArmySelected();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapRecruitmentNextArmySelected));
            Send(message);
        }

        public void OnGlobalMapRecruitmentPrevArmy()
        {
            var message = new NotifyGlobalMapRecruitmentPrevArmySelected();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyGlobalMapRecruitmentPrevArmySelected));
            Send(message);
        }

        public void OnGlobalMapCrusadeArmySquadDismiss(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot)
        {
            var message = new NotifyGlobalMapCrusadeArmySquadDismissed
            {
                SquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(globalMapArmySquadSlot),
            };
            Logger.LogInformation("Sending {MessageType}. ArmyId={SourceArmyId}, SquadId={SourceSquadId}, Position={SourcePosition}", nameof(NotifyGlobalMapCrusadeArmySquadDismissed), message.SquadSlot.ArmyId, message.SquadSlot.SquadId, message.SquadSlot.Position);
            Send(message);
        }

        public bool OnGlobalMapCrusadeArmyMergedInOne(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot)
        {
            var message = new NotifyGlobalMapCrusadeArmyMergedInOne
            {
                SquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(globalMapArmySquadSlot),
            };
            Logger.LogInformation("Sending {MessageType}. ArmyId={SourceArmyId}, SquadId={SourceSquadId}, Position={SourcePosition}", nameof(NotifyGlobalMapCrusadeArmyMergedInOne), message.SquadSlot.ArmyId, message.SquadSlot.SquadId, message.SquadSlot.Position);
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
            Logger.LogInformation("Sending {MessageType}. ArmyId={SourceArmyId}, SquadId={SourceSquadId}, Position={SourcePosition}, Count={Count}", nameof(NotifyGlobalMapCrusadeArmySquadSplitted), message.SquadSlot.ArmyId, message.SquadSlot.SquadId, message.SquadSlot.Position, count);
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
            Logger.LogInformation("Sending {MessageType}. SourceArmyId={SourceArmyId}, SourceSquadId={SourceSquadId}, SourcePosition={SourcePosition}, TargetArmyId={TargetArmyId}, TargetSquadId={TargetSquadId}, TargetPosition={TargetPosition}, Count={Count}", nameof(NotifyGlobalMapCrusadeArmySquadsMerged),
                message.SourceSquadSlot.ArmyId, message.SourceSquadSlot.SquadId, message.SourceSquadSlot.Position, message.TargetSquadSlot.ArmyId, message.TargetSquadSlot.SquadId, message.TargetSquadSlot.Position, count);
            Send(message);
        }

        public void OnGlobalMapCrusadeArmySquadsSwitched(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot)
        {
            var message = new NotifyGlobalMapCrusadeArmySquadsSwitched
            {
                SourceSquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(sourceSquadSlot),
                TargetSquadSlot = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmySquadSlot>(targetSquadSlot),
            };
            Logger.LogInformation("Sending {MessageType}. SourceArmyId={SourceArmyId}, SourceSquadId={SourceSquadId}, SourcePosition={SourcePosition}, TargetArmyId={TargetArmyId}, TargetSquadId={TargetSquadId}, TargetPosition={TargetPosition}", nameof(NotifyGlobalMapCrusadeArmySquadsSwitched),
                message.SourceSquadSlot.ArmyId, message.SourceSquadSlot.SquadId, message.SourceSquadSlot.Position, message.TargetSquadSlot.ArmyId, message.TargetSquadSlot.SquadId, message.TargetSquadSlot.Position);
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
            Logger.LogInformation("Sending {MessageType}. SourceArmyId={SourceArmyId}, SourceSquadId={SourceSquadId}, SourcePosition={SourcePosition}, TargetArmyId={TargetArmyId}, TargetSquadId={TargetSquadId}, TargetPosition={TargetPosition}, Count={Count}", nameof(NotifyGlobalMapCrusadeArmySquadSplitRequested),
                message.SourceSquadSlot.ArmyId, message.SourceSquadSlot.SquadId, message.SourceSquadSlot.Position, message.TargetSquadSlot.ArmyId, message.TargetSquadSlot.SquadId, message.TargetSquadSlot.Position, count);
            Send(message);
        }

        public override void OnStartRest()
        {
            base.OnStartRest();

            var message = new NotifyRestStarted();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyRestStarted));
            Send(message);
        }

        public bool OnAreaEffectTriggered(NetworkAreaEffect areaEffect)
        {
            if (Game.Combat != null && Game.Combat.Turn == null)
            {
                Game.Combat.TriggeredAreaEffects.Add(areaEffect);
                Logger.LogWarning("Area effect has been triggered in combat mid turn. Id={Id}, Name={Name}", areaEffect.Id, areaEffect.Name);
                PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.AreaEffects.Triggered.Key, Abstractions.GameInteraction.CombatLog.CombatTextSeverity.Debug, areaEffect.Name, areaEffect.Id);
            }

            return true;
        }

        protected override bool OnToggleOffPause(out bool showReason)
        {
            showReason = true;
            return TryEndForcedPause();
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

        private void TryStartTurn()
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
                        var combatState = CombatInteraction.GetCombatState();

                        var syncMessage = new NotifyCombatTurnSynchronizationRequired
                        {
                            TurnSeed = Game.Combat.Turn.Seed,
                            CombatState = Mapper.Map<Networking.Messages.Contracts.NetworkCombatState>(combatState),
                            TriggeredAreaEffects = Mapper.Map<List<Networking.Messages.Contracts.NetworkAreaEffect>>(Game.Combat.TriggeredAreaEffects)
                        };
                        Game.Combat.TriggeredAreaEffects.Clear();
                        Logger.LogInformation("Sending {MessageType}. TurnSeed={TurnSeed}, TriggeredAreaEffects={TriggeredAreaEffects}", syncMessage.TurnSeed, syncMessage.TriggeredAreaEffects);
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

                    var message = new NotifyCombatTurnStarted
                    {
                        Round = Game.Combat.Round,
                        UnitId = Game.Combat.Turn.UnitId
                    };

                    Send(message);

                    Game.Combat.Turn.IsInProgress = true;
                }

                CombatInteraction.StartTurnBasedCombatTurn(Game.Combat.Turn.UnitId);
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

            if (Game.ForcedPause != null)
            {
                Game.ForcedPause.ReadyPlayers.Add(Game.LocalPlayerId);
                TryEndForcedPause();
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
                    TryEndForcedPause();
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
               .On<AIActionRequest>(OnAIActionRequest)

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
               .On<ClientCombatTurnSynchronized>(OnClientCombatTurnSynchronized)

               // global map & crusade combat
               .On<NotifyGlobalMapTravelerModeChanged>(OnNotifyGlobalMapTravelerModeChanged)
               .On<NotifyTacticalCombatInitializationConfirmed>(OnNotifyTacticalCombatInitializationConfirmed)
               .On<NotifyGlobalMapCrusadeArmyMergeCartClosed>(OnNotifyGlobalMapCrusadeArmyMergeCartClosed)
               .On<NotifyGlobalMapCrusadeArmyInfoShown>(OnNotifyGlobalMapCrusadeArmyInfoShown)
               .On<NotifyGlobalMapCrusadeArmySetLeaderClosed>(OnNotifyGlobalMapCrusadeArmySetLeaderClosed)
               .On<NotifyGlobalMapCrusadeArmyBuyLeaderClosed>(OnNotifyGlobalMapCrusadeArmyBuyLeaderClosed)
               .On<NotifyGlobalMapRecruitmentShown>(OnNotifyGlobalMapRecruitmentShown)
               .On<NotifyGlobalMapRecruitmentClosed>(OnNotifyGlobalMapRecruitmentClosed)

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

        private async void OnClientCombatPreparationStarted(long receivedFrom, ClientCombatPreparationStarted message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, UnitsCount={UnitsCount}", nameof(ClientCombatPreparationCompleted), receivedFrom, message.Units.Count);
            var units = Mapper.Map<List<NetworkUnit>>(message.Units);

            await WaitWhileTrue(() => Game.Combat == null, $"Waiting for combat to start to add preparation. PlayerId={receivedFrom}");
            Game.Combat.PlayersCombatPreparation.TryAdd(receivedFrom, units);
        }

        private void OnClientCombatPreparationCompleted(long receivedFrom, ClientCombatPreparationCompleted message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, Units={Units}", nameof(ClientCombatPreparationCompleted), receivedFrom, message.PlayerId, message.Units.Select(x => x.Id));
            Game.Combat.PlayersCombatPreparation.TryRemove(message.PlayerId, out _);
            Logger.LogInformation("Combat preparation updated. ConfirmedPlayer={ConfirmedPlayer}, PlayersLeft={PlayersLeft}", message.PlayerId, Game.Combat.PlayersCombatPreparation.Keys);
        }

        private void OnClientTogglePauseOff(long receivedFrom, ClientTogglePauseOff message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(ClientTogglePauseOff), receivedFrom, message.PlayerId);

            TryEndForcedPause();
        }

        private void OnNotifyGlobalMapRecruitmentClosed(long receivedFrom, NotifyGlobalMapRecruitmentClosed message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapRecruitmentClosed), receivedFrom, message.PlayerId);
            // no need for specific removal as recruitment is already closed
            ResetPlayersTracker(Game.PlayersInGlobalMapRecruitment);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapRecruitmentShown(long receivedFrom, NotifyGlobalMapRecruitmentShown message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapRecruitmentShown), receivedFrom, message.PlayerId);

            AddPlayerToTracker(Game.PlayersInGlobalMapRecruitment, message.PlayerId);
            UpdateGlobalMapRecruitmentUIState();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapCrusadeArmyBuyLeaderClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyBuyLeaderClosed message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapTravelerModeChanged), receivedFrom, message.PlayerId);
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyBuyLeader, message.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateAfterBuyLeader();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderClosed(long receivedFrom, NotifyGlobalMapCrusadeArmySetLeaderClosed message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapTravelerModeChanged), receivedFrom, message.PlayerId);
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmySetLeader, message.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateAfterSetLeader();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoShown(long receivedFrom, NotifyGlobalMapCrusadeArmyInfoShown message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapCombatResultsShown), receivedFrom, message.PlayerId);
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyInfo, message.PlayerId);

            UpdateGlobalMapCrusadeArmyInfoUIState();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapCrusadeArmyMergeCartClosed(long receivedFrom, NotifyGlobalMapCrusadeArmyMergeCartClosed globalMapCrusadeArmyInfoMergeClosed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapTravelerModeChanged), receivedFrom, globalMapCrusadeArmyInfoMergeClosed.PlayerId);
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyInfoMerge, globalMapCrusadeArmyInfoMergeClosed.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateAfterMerge();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCrusadeArmyInfoMergeClosed);
        }

        private void OnNotifyTacticalCombatInitializationConfirmed(long receivedFrom, NotifyTacticalCombatInitializationConfirmed tacticalCombatInitializationConfirmed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapTravelerModeChanged), receivedFrom, tacticalCombatInitializationConfirmed.PlayerId);

            Game.ArmyCombat.PlayersCombatInitialization.TryAdd(tacticalCombatInitializationConfirmed.PlayerId, true);

            Game.ArmyCombat.IsInitialized = TryConfirmTacticalCombatInitialization();
        }

        private void OnNotifyGlobalMapTravelerModeChanged(long receivedFrom, NotifyGlobalMapTravelerModeChanged globalMapTravelerModeChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, TravelerMode={TravelerMode}, MustBeEnforced={MustBeEnforced}", nameof(NotifyGlobalMapTravelerModeChanged), globalMapTravelerModeChanged.PlayerId, globalMapTravelerModeChanged.TravelerMode, globalMapTravelerModeChanged.MustBeEnforced);

            var travelerMode = Mapper.Map<NetworkGlobalMapTravelerMode>(globalMapTravelerModeChanged.TravelerMode);
            RegisterGlobalMapMode(globalMapTravelerModeChanged.PlayerId, travelerMode);
            UpdateGlobalMapUIState();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapTravelerModeChanged);
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
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Reason={Reason}, RemovalDelay={RemovalDelay}", nameof(ClientGameAutoPaused), playerId, clientGameAutoPaused.Pause.Reason, clientGameAutoPaused.Pause.RemovalDelay);
            var pause = Mapper.Map<NetworkForcedPause>(clientGameAutoPaused.Pause);
            lock (ActionLock)
            {
                EnsureForcePaused(pause.Reason, pause.RemovalDelay);
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

        private async void OnAIActionRequest(long playerId, AIActionRequest request)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, ActionIndex={ActionIndex}", nameof(AIActionRequest), playerId, request.UnitId, request.ActionIndex);
            var timeout = Task.Delay(request.Timeout);
            try
            {
                NetworkAIAction networkAIAction = null;
                do
                {
                    var turnActions = (Game.Combat?.AIActions ?? Game.ArmyCombat?.AIActions ?? []).
                        Where(x => string.Equals(x.UnitId, request.UnitId, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (turnActions.Count <= request.ActionIndex)
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

        private async void OnRandomEncounterContextRequest(long receivedFrom, RandomEncounterContextRequest request)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(RandomEncounterContextRequest), receivedFrom);

            var randomEncounterIndex = request.SleepPhase - 1;
            var timeout = Task.Delay(request.Timeout);
            await WaitWhileTrue(() => !timeout.IsCompleted && (Game.Rest == null || Game.Rest.RandomEncounters.Count <= randomEncounterIndex),
                $"Rest Random Encounter is not available yet. RequestedSleepPhase={request.SleepPhase}, RandomEncounterIndex={randomEncounterIndex}");

            var encounter = timeout.IsCompleted ? null : Game.Rest?.RandomEncounters[randomEncounterIndex];
            var response = new RandomEncounterContextResponse
            {
                Encounter = Mapper.Map<Networking.Messages.Contracts.NetworkRandomEncounter>(encounter)
            };

            Logger.LogInformation("Sending {MessageType}. IsAvailable={IsAvailable}", response.Encounter != null);

            Send(receivedFrom, response);
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

        private void OnClientCombatInitializationCompleted(long playerId, ClientCombatInitializationCompleted message)
        {
            Logger.LogInformation("Received {MessageType}", nameof(ClientCombatInitializationCompleted), playerId);

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

            Send(message);
        }

        private void OnClientDialogCueAnswerSuggested(long playerId, ClientDialogCueAnswerSuggested clientDialogCueAnswerSuggested)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, DialogName={DialogName}, CueName={CueName}, AnswerName={AnswerName}", nameof(ClientDialogCueAnswerSuggested), playerId, clientDialogCueAnswerSuggested.DialogName, clientDialogCueAnswerSuggested.CueName, clientDialogCueAnswerSuggested.AnswerName);

            if (Game.Dialog == null)
            {
                Logger.LogError("Received dialog answer suggestion, but there is no active dialog right now. SuggestedDialogName={SuggestedDialogName}, SuggestedCueName={SuggestedCueName}, SuggestedAnswer={SuggestedAnswer}", clientDialogCueAnswerSuggested.DialogName, clientDialogCueAnswerSuggested.CueName, clientDialogCueAnswerSuggested.AnswerName);
                return;
            }

            if (!string.Equals(Game.Dialog.Name, clientDialogCueAnswerSuggested.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog suggestion has mismatched dialog name. SuggestedDialogName={SuggestedDialogName}, CurrentDialogName={CurrentDialogName}", clientDialogCueAnswerSuggested.DialogName, Game.Dialog.Name);
                return;
            }

            if (!string.Equals(Game.Dialog.CurrentCueName, clientDialogCueAnswerSuggested.CueName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog suggestion has mismatched cue name. SuggestedCueName={SuggestedCueName}, CurrentCueName={CurrentCueName}", clientDialogCueAnswerSuggested.CueName, Game.Dialog.CurrentCueName);
                return;
            }

            Game.Dialog.AnswerSuggestions.AddOrUpdate(playerId, clientDialogCueAnswerSuggested.AnswerName, (key, existing) =>
            {
                return clientDialogCueAnswerSuggested.AnswerName;
            });

            List<NetworkDialogAnswerSuggestion> suggestions = [.. Game.Dialog.AnswerSuggestions.GroupBy(x => x.Value, x => x.Key).Select(x => new NetworkDialogAnswerSuggestion { AnswerName = x.Key, Players = [.. x] })];
            DialogInteraction.MarkSuggestedDialogAnswers(suggestions);

            var notifyMessage = new NotifyDialogCueAnswerSuggested
            {
                DialogName = clientDialogCueAnswerSuggested.DialogName,
                CueName = clientDialogCueAnswerSuggested.CueName,
                Suggestions = Mapper.Map<List<Networking.Messages.Contracts.NetworkDialogAnswerSuggestion>>(suggestions),
            };
            Send(notifyMessage);
        }

        private void OnClientDialogCueWitnessed(long playerId, ClientDialogCueWitnessed clientDialogCueWitnessed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, DialogName={DialogName}, CueName={CueName}", nameof(ClientDialogCueWitnessed), playerId, clientDialogCueWitnessed.DialogName, clientDialogCueWitnessed.CueName);
            if (Game.Dialog == null)
            {
                Logger.LogError("Received cue witness, but there is no active dialog right now. WitnessedDialogName={WitnessedDialogName}, WitnessedCueName={WitnessedCueName}", clientDialogCueWitnessed.DialogName, clientDialogCueWitnessed.CueName);
                return;
            }

            if (!string.Equals(Game.Dialog.Name, clientDialogCueWitnessed.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Cue witness has mismatched dialog. WitnessedDialogName={WitnessedDialogName}, CurrentDialogName={CurrentDialogName}", clientDialogCueWitnessed.DialogName, Game.Dialog.Name);
                return;
            }

            AddCueWitness(clientDialogCueWitnessed.CueName, playerId);
            TryEnableDialogContinueButton();
        }

        private async void OnDiceRollValueRequest(long playerId, DiceRollValueRequest request)
        {
            try
            {
                Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, RollId={RollId}, UnitId={UnitId}, RuleName={RuleName}, CombatTurnUnitId={CombatTurnUnitId}", nameof(DiceRollValueRequest), playerId, request.RollId, request.UnitId, request.RuleName, request.CombatTurnUnitId);
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
                Logger.LogInformation("Sending roll to a client. PlayerId={PlayerId}, RollId={RollId}", playerId, rollFromAnotherClient.RollId);
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
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, Status={Status}", nameof(NotifyLobbySyncStatusChanged), receivedFrom, lobbySyncStatusChanged.PlayerId, lobbySyncStatusChanged.Status);

            var status = Mapper.Map<NetworkLobbySyncStatus>(lobbySyncStatusChanged.Status);
            UpdateLobbySyncStatus(lobbySyncStatusChanged.PlayerId, status);

            TryStartSavedGame();

            OnAfterNetworkMessageHandled(receivedFrom, lobbySyncStatusChanged);
        }

        private void OnClientAreaLoaded(long receivedFrom, ClientAreaLoaded clientAreaLoaded)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}", nameof(ClientAreaLoaded), receivedFrom);
            lock (ActionLock)
            {
                EnsureForcePaused(NetworkForcedPauseReason.AreaLoading);
                Game.ForcedPause.ReadyPlayers.Add(receivedFrom);
            }

            TryEndForcedPause();
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
                Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, Settings={Settings}", nameof(GameServerConnectionSucceeded), message.ClientPlayerId, message.GameSettings);

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
                Logger.LogInformation("Sending {MessageType}", nameof(NotifyLobbyPlayersChanged));
                _networkServer.SendAllExcept(playerId, playersChanged);
                ShowPlayerDisconnectedMessage(removedPlayer);

                TryEnableDialogContinueButton();

                RefreshUIOnPlayerDisconnect(removedPlayer.Id);

                TryEndForcedPause();
            }
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
                    Send(playersChanged);

                    var lobbyCharactersChanged = new NotifyLobbyCharactersChanged
                    {
                        Title = Game.StartUp?.Title,
                        Characters = Mapper.Map<List<Networking.Messages.Contracts.NetworkCharacter>>(Game.Characters)
                    };
                    Logger.LogInformation("Sending {MessageType} to new player. PlayerId={PlayerId}", nameof(NotifyLobbyCharactersChanged), playerId);
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
                    QuickMovement = true
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
                    var missingPlayer = GetPlayers().Where(x => !Game.ForcedPause.ReadyPlayers.Contains(x.Id)).ToList();
                    if (missingPlayer.Any())
                    {
                        Logger.LogInformation("Not everyone is ready, forced pause will remain. MissingPlayers={MissingPlayers}", missingPlayer.Select(x => x.Name));
                        return false;
                    }

                    var removalDelay = Game.ForcedPause.RemovalDelay;
                    var delay = removalDelay.HasValue ? Task.Delay(removalDelay.Value) : Task.CompletedTask;
                    Game.ForcedPause.IsLifting = true;
                    Logger.LogInformation("Forced pause will be lifted soon. Delay={Delay}", removalDelay.GetValueOrDefault());
                    delay.ContinueWith(x =>
                    {
                        var message = new NotifyGamePauseEnded
                        {
                            AreaSeed = Game.ForcedPause.Reason == NetworkForcedPauseReason.AreaLoading ? Game.CurrentArea.Seed : null
                        };
                        Logger.LogInformation("Sending {MessageType}. AreaSeed={AreaSeed}", nameof(NotifyGamePauseEnded), message.AreaSeed);
                        Send(message);

                        Game.ForcedPause = null;
                        GameInteraction.SetPause(false);
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
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, Type={Type}", nameof(NotifyLevelingStarted), playerId, message.UnitId, message.Type);

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
