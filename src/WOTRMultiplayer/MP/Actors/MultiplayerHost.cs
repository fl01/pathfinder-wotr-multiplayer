using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Leveling;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Settings;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Vendor;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.Networking.Messages.Requests;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer.MP.Actors
{
    public class MultiplayerHost : MultiplayerActorBase, IMultiplayerHost
    {
        private readonly INetworkServer _networkServer;

        private NetworkGameStage Status => Game?.Stage ?? NetworkGameStage.None;

        public Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }

        public Action<NetworkGameConnectivity> OnConnected { get; set; }

        public bool IsActive => _networkServer.IsActive;

        public bool IsInLobby => IsActive && Status == NetworkGameStage.Lobby;

        protected override bool IsHost => true;

        public MultiplayerHost(
            ILogger<MultiplayerHost> logger,
            IGameInteractionService gameInteractionService,
            IMultiplayerSettingsProvider multiplayerSettingsProvider,
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
                  valueGenerator)
        {
            _networkServer = networkServer;
        }

        public void Create(string saveFilePath, string gameId, List<NetworkCharacterOwnership> characters)
        {
            if (_networkServer.IsActive)
            {
                _networkServer.Reset();
            }

            RegisterHandlers();

            Game?.Reset();

            Game = new NetworkGame(saveFilePath)
            {
                LocalPlayerId = LocalHostPlayerId,
                Id = gameId,
                RestBanterSeed = new System.Random().Next(int.MinValue, int.MaxValue)
            };

            Game.Characters.AddRange(characters);

            _networkServer.Start(SettingsProvider.Settings.HostPortRangeStart, SettingsProvider.Settings.HostPortRangeEnd);

            Logger.LogInformation("Host has been created. SavePath={SavePath}, Portraits={Portraits}", saveFilePath, string.Join(";", Game.Characters.Select(c => c.Portrait)));
        }

        public void UpdateSaveGame(string saveFilePath, string gameId, List<NetworkCharacterOwnership> characters)
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

        public void ChangeCharacterOwner(int characterIndex, int playerIndex)
        {
            lock (ActionLock)
            {
                if (Game.Players.Count < playerIndex)
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

                var charactersOwnerChanged = CreateNotifyCharactersOwnerChanged();
                _networkServer.SendAll(charactersOwnerChanged);
            }
        }

        public void Reset()
        {
            Logger.LogInformation("Resetting");

            Game?.Reset();
            _networkServer.Reset();
        }

        public bool ReadyChanged()
        {
            var player = Game.Players.First(p => p.Id == Game.LocalPlayerId); // host should be always present
            var readyChanged = new PlayerReadyStatusChanged { PlayerId = player.Id, IsReady = !player.IsReady };
            OnPlayerReadyStatusChanged(player.Id, readyChanged);
            return readyChanged.IsReady;
        }

        public void Start()
        {
            Logger.LogInformation("Starting game...");
            // it should be fine to block current thread
            var content = FileSystem.GetFile(Game.SaveFilePath);
            if (content == null)
            {
                Logger.LogError("Unable to start a game due to missing save file. Path={Path}", Game.SaveFilePath);
                return;
            }

            Game.Stage = NetworkGameStage.Initializing;
            var gameStageChanged = new NotifyGameStageChanged { Stage = Game.Stage.ToString() };
            _networkServer.SendAll(gameStageChanged);

            lock (ActionLock)
            {
                var saveGameMessageAssigned = new NotifySaveGameAssigned { GameId = Game.Id, Content = content, IsForceLoad = false };
                Logger.LogInformation("Sending save game file content to all players. Size={Size}", saveGameMessageAssigned.Content.Length);
                _networkServer.SendAll(saveGameMessageAssigned);
                Game.Stage = NetworkGameStage.WaitingForPlayersInitialization;
                Logger.LogInformation("Waiting for players to confirm delivery. GameStatus={GameStatus}", Game.Stage);
                GetHost().IsSyncedToStartGame = true;
            }

            TryStartGame();
        }

        public override void OnAreaScenesLoaded()
        {
            base.OnAreaScenesLoaded();

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
            // confirmation from host is required
            if (Game.Combat == null)
            {
                return true;
            }

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

            if (Game.Combat.Round == 1 && !Game.Combat.IsInitialized)
            {
                var unitsInCombat = GameInteraction.GetUnitsInCombat();
                var message = new NotifyCombatInitialized
                {
                    Units = Mapper.Map<List<Networking.Messages.Contracts.NetworkUnit>>(unitsInCombat)
                };
                _networkServer.SendAll(message);
                Game.Combat.IsInitialized = true;
                Game.Combat.PlayersCombatInitialization.TryAdd(Game.LocalPlayerId, true);
                Logger.LogInformation("Sending {MessageType}. UnitsInCombat={UnitsInCombat}", nameof(NotifyCombatInitialized), message.Units.Count);
            }

            var canContinue = Game.Combat.PlayersCombatInitialization.Count >= Game.Players.Count;
            return canContinue;
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
                    EnsureForcePaused(UIStringConsts.GameNotifications.ForcedPauseReasons.RandomEncounterLoading, SettingsProvider.Settings.ForcedPauseRandomEncounterTerminationDelay);
                    GameInteraction.UpdateIsInCombatStatus();
                    GameInteraction.Pause(true);
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
                if (Game.Combat == null || !SettingsProvider.Settings.EnableCombatAIActionsSync)
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

        public void OnCharacterLevelingStarted(string unitId)
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
            var totalPlayersCount = Game.Players.Count;
            var isInteractable = readyPlayersCount >= totalPlayersCount;
            GameInteraction.SetStartRestButtonState(isInteractable, readyPlayersCount, totalPlayersCount);
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

        protected override bool OnStopGameModeInternal(GameModeType type)
        {
            var playerId = GetLocalPlayerId();
            var isFirstTime = UnregisterGameMode(type, playerId);

            if (isFirstTime && type == GameModeType.Rest && Game.ForcedPause != null)
            {
                lock (ActionLock)
                {
                    Game.ForcedPause.ReadyPlayers.Add(playerId);
                    GameInteraction.Pause(true);
                    TryEndForcedPause();
                }
            }

            return true;
        }

        protected override DiceRollValueResponse RetrieveRoll(DiceRollValueRequest request)
        {
            // the only case when host is retrieving rolls - he is not the turn owner + it's not AI turn
            var character = GetCharacterOwnership(Game.Combat.Turn.UnitId);
            if (character?.Owner == null)
            {
                Logger.LogError("Unable to retrieve roll due to missing character ownership. UnitId={UnitId}");
                return null;
            }

            if (character.Owner.Id == LocalHostPlayerId)
            {
                Logger.LogError("Host is character owner, but tries to retrieve network roll");
                return null;
            }

            return _networkServer.SendAndWaitFor<DiceRollValueResponse>(character.Owner.Id, request);
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
            Logger.LogInformation("Checking if turn could be started. Round={Round}, UnitId={UnitId}", Game.Combat.Round, Game.Combat.Turn?.UnitId);

            lock (ActionLock)
            {
                if (Game.Combat.Turn.IsInProgress)
                {
                    Logger.LogInformation("Previous turn is in progress, can't start yet");
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
                        GameInteraction.AddCombatText(string.Format(UIStringConsts.GameNotifications.CombatLog.HostDetectedDesyncInCombatTurnOrder, player?.Name));

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
                    var unitsToSync = GameInteraction.GetUnitsInCombat();
                    var syncMessage = new NotifyCombatTurnSynchronizationRequired
                    {
                        Units = Mapper.Map<List<Networking.Messages.Contracts.NetworkUnit>>(unitsToSync),
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

        protected override void OnLocalPlayerTurnEnded()
        {
            var message = new PlayerCombatTurnEnded { UnitId = Game.Combat.Turn.UnitId };
            _networkServer.SendAll(message);
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

            var players = Game.Players.Where(p => !cueViews.Contains(p.Id)).ToList();
            return players;
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

        private void TryEndForcedPause()
        {
            try
            {
                Logger.LogInformation("Checking if forced pause could be removed. PauseIsNull={PauseIsNull}", Game.ForcedPause == null);

                if (Game.ForcedPause == null)
                {
                    return;
                }

                lock (ActionLock)
                {
                    var allReady = Game.ForcedPause.ReadyPlayers.Count >= Game.Players.Count;
                    if (!allReady)
                    {
                        Logger.LogInformation("Not everyone is ready, forced pause will remain. ReadyPlayers={ReadyPlayers}", Game.ForcedPause.ReadyPlayers);
                        return;
                    }

                    Game.Stage = NetworkGameStage.Playing;
                    var removalDelay = Game.ForcedPause.RemovalDelay;
                    var delay = removalDelay.HasValue ? Task.Delay(removalDelay.Value) : Task.CompletedTask;
                    Game.ForcedPause = null;

                    Logger.LogInformation("Forced pause will be lifted soon. Delay={Delay}", removalDelay.GetValueOrDefault());
                    delay.ContinueWith(x =>
                    {
                        GameInteraction.Pause(false);
                        var message = new NotifyForcedPauseEnded();
                        _networkServer.SendAll(message);
                    });
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
                EnsureForcePaused(UIStringConsts.GameNotifications.ForcedPauseReasons.AreaLoading);
                Game.ForcedPause.ReadyPlayers.Add(playerId);
            }

            TryEndForcedPause();
        }

        private void TryStartGame()
        {
            var canStart = false;

            lock (ActionLock)
            {
                canStart = Game.Players.All(p => p.IsSyncedToStartGame);
            }

            if (canStart)
            {
                Logger.LogInformation("Starting game");
                _networkServer.SendAll(new NotifyGameStarted());
                InvokeOnStartGame();
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

        private NetworkPlayer GetHost()
        {
            return Game.Players.First(f => f.Id == Game.LocalPlayerId);
        }

        private void RegisterHandlers()
        {
            _networkServer.OnClientConnected = OnPlayerConnected;
            _networkServer.OnClientDisconnected = OnPlayerDisconnected;
            _networkServer.OnServerStarted = OnServerStarted;

            _networkServer
                // this is kinda special because requester is blocking the thread (most likely game main loop) until <see cref="DiceRollValueResponse"/> is received
                .On<DiceRollValueRequest>(OnDiceRollValueRequest)
                .On<DiceRollValueResponse>(null) // usable as awaiter only
                .On<RandomEncounterContextRequest>(OnRandomEncounterContextRequest)
                .On<AIActionRequest>(OnAIActionRequest)

                .On<NotifySaveGameAssigned>(OnNotifySaveGameAssigned)
                .On<NotifyUnitClicked>(OnNotifyUnitClicked)
                .On<NotifyGroundClicked>(OnNotifyGroundClicked)
                .On<NotifyMapObjectClicked>(OnNotifyMapObjectClicked)
                .On<PlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
                .On<ClientGameServerConnectionConfirmed>(OnPlayerNameResponse)
                .On<PlayerSaveGameSyncChanged>(OnPlayerSaveGameSyncChanged)
                .On<NotifyCharacterMove>(OnNotifyCharacterMove)
                .On<ClientAreaLoaded>(OnClientAreaLoaded)
                .On<CueWitnessed>(OnCueWitnessed)
                .On<DialogCueAnswerSuggested>(OnDialogCueAnswerSuggested)
                .On<ClientDialogStartRequested>(OnClientDialogStartRequested)
                .On<NotifyAbilityUse>(OnNotifyAbilityUsed)
                .On<NotifyToggleActivatableAbility>(OnNotifyToggleActivatableAbility)
                .On<PlayerCombatTurnEnded>(OnPlayerCombatTurnEnded)
                .On<ClientCombatInitialized>(OnClientCombatInitialized)
                .On<ClientCombatTurnStarted>(OnClientCombatTurnStarted)
                .On<ClientCombatTurnSynchronized>(OnClientCombatTurnSynchronized)
                .On<NotifyContainerLooted>(OnNotifyContainerLooted)
                .On<NotifyDropItem>(OnNotifyDropItem)
                .On<NotifyEquipmentSlotChanged>(OnNotifyEquipmentSlotChanged)
                .On<NotifyActiveHandEquipmentSetChanged>(OnNotifyActiveHandEquipmentSetChanged)
                .On<NotifyOvertipInteracted>(OnNotifyOvertipInteracted)
                .On<NotifyUnitJoinedMidCombat>(OnNotifyUnitJoinedMidCombat)
                .On<ClientGameModeTypeStarted>(OnClientGameModeTypeStarted)
                .On<ClientGameModeTypeEnded>(OnClientGameModeTypeEnded)
                .On<ClientRestEnded>(OnClientRestEnded)
                .On<NotifyRestBanterInterrupted>(OnNotifyRestBanterInterrupted)
                .On<NotifyVendorItemTransferred>(OnNotifyVendorItemTransferred)
                .On<NotifySpellMemorized>(OnNotifySpellMemorized)
                .On<NotifySpellForgotten>(OnNotifySpellForgotten)

                // leveling
                .On<ClientCharacterLevelingRequested>(OnClientCharacterLevelingRequested)
                .On<NotifyLevelingClassArchetypeSelected>(OnNotifyLevelingClassArchetypeSelected)
                .On<NotifyLevelingClassSelected>(OnNotifyLevelingClassSelected)
                .On<NotifyLevelingPhaseWitnessed>(OnNotifyLevelingPhaseWitnessed)
                .On<NotifyLevelingPhaseChanged>(OnNotifyLevelingPhaseChanged)
                .On<NotifyLevelingSkillPointIncreased>(OnNotifyLevelingSkillPointIncreased)
                .On<NotifyLevelingSkillPointDecreased>(OnNotifyLevelingSkillPointDecreased)
                .On<NotifyLevelingAbilityScoreIncreased>(OnNotifyLevelingAbilityScoreIncreased)
                .On<NotifyLevelingAbilityScoreDecreased>(OnNotifyLevelingAbilityScoreDecreased)
                .On<NotifyLevelingFeatureSelected>(OnNotifyLevelingFeatureSelected)
                .On<NotifyLevelingSpellChosen>(OnNotifyLevelingSpellChosen)
                .On<NotifyLevelingSpellRemoved>(OnNotifyLevelingSpellRemoved)
                .On<NotifyLevelingCompleted>(OnNotifyLevelingCompleted)
                .On<NotifyLevelingTerminated>(OnNotifyLevelingTerminated)
                ;
        }

        private void OnNotifyLevelingAbilityScoreDecreased(long playerId, NotifyLevelingAbilityScoreDecreased decreased)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, StatType={StatType}", nameof(NotifyLevelingAbilityScoreDecreased), playerId, decreased.AbilityScore.StatType);

            var abilityScore = Mapper.Map<NetworkLevelingAbilityScore>(decreased.AbilityScore);
            GameInteraction.DecreaseLevelingAbilityScore(abilityScore);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingAbilityScoreDecreased));
            _networkServer.SendAllExcept(playerId, decreased);
        }

        private void OnNotifyLevelingAbilityScoreIncreased(long playerId, NotifyLevelingAbilityScoreIncreased increased)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, StatType={StatType}", nameof(NotifyLevelingAbilityScoreIncreased), playerId, increased.AbilityScore.StatType);

            var abilityScore = Mapper.Map<NetworkLevelingAbilityScore>(increased.AbilityScore);
            GameInteraction.IncreaseLevelingAbilityScore(abilityScore);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingAbilityScoreIncreased));
            _networkServer.SendAllExcept(playerId, increased);
        }

        private void OnNotifyLevelingCompleted(long playerId, NotifyLevelingCompleted completed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyLevelingCompleted), playerId);
            GameInteraction.CompleteLeveling();

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingCompleted));
            _networkServer.SendAllExcept(playerId, completed);
        }

        private void OnNotifyLevelingTerminated(long playerId, NotifyLevelingTerminated terminated)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyLevelingTerminated), playerId);
            GameInteraction.TerminateLeveling();

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingTerminated));
            _networkServer.SendAllExcept(playerId, terminated);
        }

        private void OnNotifyLevelingSpellRemoved(long playerId, NotifyLevelingSpellRemoved removed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, SpellName={SpellName}, SpellId={SpellId}", nameof(NotifyLevelingSpellRemoved), playerId, removed.Spell.Name, removed.Spell.Id);
            var spell = Mapper.Map<NetworkLevelingSpell>(removed.Spell);
            GameInteraction.RemoveLevelingSpell(spell);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingSpellRemoved));
            _networkServer.SendAllExcept(playerId, removed);
        }

        private void OnNotifyLevelingSpellChosen(long playerId, NotifyLevelingSpellChosen chosen)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, SpellName={SpellName}, SpellId={SpellId}", nameof(NotifyLevelingSpellChosen), playerId, chosen.Spell.Name, chosen.Spell.Id);
            var spell = Mapper.Map<NetworkLevelingSpell>(chosen.Spell);
            GameInteraction.SelectLevelingSpell(spell);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingSpellChosen));
            _networkServer.SendAllExcept(playerId, chosen);
        }

        private void OnNotifyLevelingFeatureSelected(long playerId, NotifyLevelingFeatureSelected selected)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, FeatureName={FeatureName}, FeatureId={FeatureId}", nameof(NotifyLevelingFeatureSelected), playerId, selected.Feature.Name, selected.Feature.Id);
            var feature = Mapper.Map<NetworkLevelingFeature>(selected.Feature);
            GameInteraction.SelectLevelingFeature(feature);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingFeatureSelected));
            _networkServer.SendAllExcept(playerId, selected);
        }

        private void OnNotifyLevelingSkillPointDecreased(long playerId, NotifyLevelingSkillPointDecreased decreased)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, StatType={StatType}", nameof(NotifyLevelingSkillPointDecreased), playerId, decreased.Skill.StatType);
            var skillPoint = Mapper.Map<NetworkLevelingSkillPoint>(decreased.Skill);
            GameInteraction.DecreaseLevelingSkillPoint(skillPoint);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingSkillPointDecreased));
            _networkServer.SendAllExcept(playerId, decreased);
        }

        private void OnNotifyLevelingSkillPointIncreased(long playerId, NotifyLevelingSkillPointIncreased increased)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, StatType={StatType}", nameof(NotifyLevelingSkillPointIncreased), playerId, increased.Skill.StatType);
            var skillPoint = Mapper.Map<NetworkLevelingSkillPoint>(increased.Skill);
            GameInteraction.IncreaseLevelingSkillPoint(skillPoint);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingSkillPointIncreased));
            _networkServer.SendAllExcept(playerId, increased);
        }

        private void OnNotifyLevelingPhaseChanged(long playerId, NotifyLevelingPhaseChanged changed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Index={Index}", nameof(NotifyLevelingPhaseChanged), playerId, changed.Phase.Index);
            var phase = Mapper.Map<NetworkLevelingPhase>(changed.Phase);
            Game.Leveling.PlayerReadiness.Clear();
            GameInteraction.SwitchLevelingPhase(phase);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingPhaseChanged));
            _networkServer.SendAllExcept(playerId, changed);
        }

        private void OnNotifyLevelingPhaseWitnessed(long playerId, NotifyLevelingPhaseWitnessed witnessed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyLevelingPhaseWitnessed), playerId);
            WitnessLevelingPhase(playerId);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingPhaseWitnessed));
            _networkServer.SendAllExcept(playerId, witnessed);
        }

        private void OnNotifyLevelingClassArchetypeSelected(long playerId, NotifyLevelingClassArchetypeSelected selected)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, ArchetypeId={ArchetypeId}", nameof(NotifyLevelingClassArchetypeSelected), playerId, selected.ArchetypeId);
            GameInteraction.SelectLevelingClassArchetype(selected.ArchetypeId);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingClassArchetypeSelected));
            _networkServer.SendAllExcept(playerId, selected);
        }

        private void OnNotifyLevelingClassSelected(long playerId, NotifyLevelingClassSelected selected)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, ClassId={ClassId}", nameof(NotifyCharacterLevelingStarted), playerId, selected.ClassId);
            GameInteraction.SelectLevelingClass(selected.ClassId);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyLevelingClassSelected));
            _networkServer.SendAllExcept(playerId, selected);
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

        private void OnNotifySpellForgotten(long playerId, NotifySpellForgotten spellForgotten)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, SpellbookId={SpellbookId}, SpellSlotIndex={SpellSlotIndex}, SpellSlotType={SpellSlotType}",
                nameof(NotifySpellForgotten), playerId, spellForgotten.Slot.UnitId, spellForgotten.Slot.SpellbookId, spellForgotten.Slot.Index, spellForgotten.Slot.Type);

            var slot = Mapper.Map<NetworkSpellSlot>(spellForgotten.Slot);

            GameInteraction.ForgetSpell(slot);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifySpellForgotten));
            _networkServer.SendAllExcept(playerId, spellForgotten);
        }

        private void OnNotifySpellMemorized(long playerId, NotifySpellMemorized memorized)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, SpellbookId={SpellbookId}, SpellId={SpellId}, SpellLevel={SpellLevel}, SpellName={SpellName}, SpellSlotIndex={SpellSlotIndex}, SpellSlotType={SpellSlotType}",
                nameof(NotifySpellMemorized), playerId, memorized.Slot.UnitId, memorized.Slot.SpellbookId, memorized.Slot.SpellId, memorized.Slot.SpellLevel, memorized.Slot.SpellName, memorized.Slot.Index, memorized.Slot.Type);

            var slot = Mapper.Map<NetworkSpellSlot>(memorized.Slot);

            GameInteraction.MemorizeSpell(slot);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifySpellMemorized));
            _networkServer.SendAllExcept(playerId, memorized);
        }

        private void OnNotifyVendorItemTransferred(long playerId, NotifyVendorItemTransferred message)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, ItemId={ItemId}, Count={Count}, Action={Action}, ActionTarget={ActionTarget}", nameof(NotifyVendorItemTransferred), playerId, message.ItemTransfer.Item.UniqueId, message.ItemTransfer.Count, message.ItemTransfer.ItemAction, message.ItemTransfer.ItemActionTarget);

            var transfer = Mapper.Map<NetworkVendorItemTransfer>(message.ItemTransfer);
            GameInteraction.TransferVendorItem(transfer);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyOvertipInteracted));
            _networkServer.SendAllExcept(playerId, message);
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

        private void OnNotifyRestBanterInterrupted(long playerId, NotifyRestBanterInterrupted interrupted)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, SpeakerUnitId={SpeakerUnitId}, Key={Key}", nameof(NotifyRestBanterInterrupted), playerId, interrupted.Banter.SpeakerUnitId, interrupted.Banter.Key);
            var banter = Mapper.Map<NetworkRestBanter>(interrupted.Banter);
            GameInteraction.TryInterruptRestBanter(banter);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyOvertipInteracted));
            _networkServer.SendAllExcept(playerId, interrupted);
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
            if (gameMode == GameModeType.Rest && Game.ForcedPause != null)
            {
                lock (ActionLock)
                {
                    Game.ForcedPause.ReadyPlayers.Add(playerId);
                    TryEndForcedPause();
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
        }

        private void OnNotifyUnitJoinedMidCombat(long playerId, NotifyUnitJoinedMidCombat combat)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}", nameof(NotifyUnitJoinedMidCombat), playerId, combat.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitJoinedMidCombat, combat.PlayerId, combat.UnitId);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyUnitJoinedMidCombat));
            _networkServer.SendAllExcept(playerId, combat);
        }

        private void OnNotifyOvertipInteracted(long playerId, NotifyOvertipInteracted interacted)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, MapObjectId={MapObjectId}, UnitsCount={UnitsCount}", nameof(NotifyOvertipInteracted), playerId, interacted.Overtip.MapObject.Id, interacted.Overtip.Units);
            var overtip = Mapper.Map<NetworkOvertip>(interacted.Overtip);
            GameInteraction.InteractWithOvertip(overtip);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyOvertipInteracted));
            _networkServer.SendAllExcept(playerId, interacted);
        }

        private void OnNotifyActiveHandEquipmentSetChanged(long playerId, NotifyActiveHandEquipmentSetChanged changed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, SetIndex={SetIndex}", nameof(NotifyEquipmentSlotChanged), playerId, changed.Set.UnitId, changed.Set.Index);
            var set = Mapper.Map<NetworkActiveHandEquipmentSet>(changed.Set);
            GameInteraction.SetActiveHandEquipmentSet(set);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyActiveHandEquipmentSetChanged));
            _networkServer.SendAllExcept(playerId, changed);
        }

        private void OnNotifyEquipmentSlotChanged(long playerId, NotifyEquipmentSlotChanged slotChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, SlotType={SlotType}, SlotIndex={SlotIndex}, ItemId={ItemId}, OwnerId={OwnerId}", nameof(NotifyEquipmentSlotChanged), playerId, slotChanged.Slot.Position.Type, slotChanged.Slot.Position.Index, slotChanged.Slot.Item?.UniqueId, slotChanged.Slot.OwnerId);
            var slot = Mapper.Map<NetworkEquipmentSlot>(slotChanged.Slot);
            GameInteraction.UpdateEquipmentSlot(slot);

            Logger.LogInformation("Resending {MessageType}", nameof(NetworkEquipmentSlot));
            _networkServer.SendAllExcept(playerId, slotChanged);
        }

        private void OnNotifyDropItem(long playerId, NotifyDropItem item)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, OwnerId={OwnerId}, ItemId={ItemId}, ItemName={ItemName}", nameof(NotifyDropItem), playerId, item.Drop.OwnerEntityId, item.Drop.Item.UniqueId, item.Drop.Item.Name);

            var dropItem = Mapper.Map<NetworkDropItem>(item.Drop);
            GameInteraction.DropItem(dropItem);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyDropItem));
            _networkServer.SendAllExcept(playerId, item);
        }

        private void OnNotifyContainerLooted(long playerId, NotifyContainerLooted looted)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, ContainerId={ContainerId}, ContainerPosition={ContainerPosition}, ItemsCount={ItemsCount}, Items={Items}",
               nameof(NotifyContainerLooted), playerId, looted.Container.Id, looted.Container.Position, looted.Container.Items.Count, looted.Container.Items.Select(i => i.UniqueId));

            var container = Mapper.Map<NetworkLootContainer>(looted.Container);
            GameInteraction.CollectContainerLoot(container);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyContainerLooted));
            _networkServer.SendAllExcept(playerId, looted);
        }

        private void OnNotifyToggleActivatableAbility(long playerId, NotifyToggleActivatableAbility activatableAbility)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, AbilityId={AbilityId}, IsActive={IsActive}", nameof(NotifyToggleActivatableAbility), playerId, activatableAbility.Ability.Id, activatableAbility.Ability.IsActive);

            var ability = Mapper.Map<NetworkActivatableAbility>(activatableAbility.Ability);
            GameInteraction.ToggleActivatableAbility(ability);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyToggleActivatableAbility));
            _networkServer.SendAllExcept(playerId, activatableAbility);
        }

        private void OnNotifyAbilityUsed(long playerId, NotifyAbilityUse abilityUse)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, AbilityId={AbilityId}", nameof(NotifyAbilityUse), playerId, abilityUse.Ability.Id);

            var ability = Mapper.Map<NetworkAbility>(abilityUse.Ability);
            GameInteraction.UseAbility(ability);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyAbilityUse));
            _networkServer.SendAllExcept(playerId, abilityUse);
        }

        private void OnClientCombatTurnSynchronized(long playerId, ClientCombatTurnSynchronized synchronized)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}", nameof(ClientCombatTurnSynchronized), playerId, synchronized.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitSynchronization, playerId, synchronized.UnitId);
            TryStartTurn();
        }

        private void OnPlayerCombatTurnEnded(long playerId, PlayerCombatTurnEnded ended)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}", nameof(PlayerCombatTurnEnded), playerId, ended.UnitId);

            _networkServer.SendAllExcept(playerId, ended);

            if (!string.Equals(Game.Combat.Turn?.UnitId, ended.UnitId, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning("Player ended invalid turn. PlayerId={PlayerId}, PlayerUnitId={PlayerUnitId}, LocalUnitId={LocalUnitId}", playerId, ended.UnitId, Game.Combat.Turn?.UnitId);
                return;
            }

            EndLocalTurn();
        }

        private void OnClientCombatTurnStarted(long playerId, ClientCombatTurnStarted started)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Round={Round}, UnitId={UnitId}", nameof(ClientCombatTurnStarted), playerId, started.Round, started.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.Start, playerId, started.UnitId);

            TryStartTurn();
        }

        private void OnNotifyGroundClicked(long playerId, NotifyGroundClicked clicked)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, SelectedUnits={SelectedUnits}, WorldPosition={WorldPosition}", nameof(NotifyGroundClicked), playerId, clicked.Click.SelectedUnits.Count, clicked.Click.WorldPosition);
            if (Game.Combat == null)
            {
                Logger.LogWarning($"{nameof(NotifyGroundClicked)} is ignored out of combat");
                return;
            }

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickGroundInCombat(click);

            Logger.LogInformation("Resending {MessageType} to other players", nameof(NotifyGroundClicked));
            _networkServer.SendAllExcept(playerId, click);
        }

        private void OnNotifyUnitClicked(long playerId, NotifyUnitClicked clicked)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, TargetUnitId={TargetUnitId}, SelectedUnits={SelectedUnits}", nameof(NotifyUnitClicked), playerId, clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickUnit(click);

            Logger.LogInformation("Resending {MessageType} to other players", nameof(NotifyUnitClicked));
            _networkServer.SendAllExcept(playerId, click);
        }

        private void OnNotifyMapObjectClicked(long playerId, NotifyMapObjectClicked clicked)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, TargetUnitId={TargetUnitId}, SelectedUnits={SelectedUnits}", nameof(NotifyMapObjectClicked), playerId, clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickMapObject(click);

            Logger.LogInformation("Resending {MessageType} to other players", nameof(NotifyMapObjectClicked));
            _networkServer.SendAllExcept(playerId, clicked);
        }

        private void OnNotifySaveGameAssigned(long playerId, NotifySaveGameAssigned assigned)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, IsForceLoad={IsForceLoad}, SaveGameSize={SaveGameSize}", nameof(NotifySaveGameAssigned), playerId, assigned.IsForceLoad, assigned.Content.Length);

            Logger.LogInformation("Resending {MessageType} to other players", nameof(NotifySaveGameAssigned));
            _networkServer.SendAllExcept(playerId, assigned);

            Game.SaveFilePath = StoreSaveFile(assigned.Content);
            if (string.IsNullOrEmpty(Game.SaveFilePath))
            {
                Logger.LogError("Unable to store save game");
                // on error?
                return;
            }

            if (assigned.IsForceLoad)
            {
                ForceLoadGame();
            }
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
                && character.Owner.Id != LocalHostPlayerId
                && character.Owner.Id != playerId)
            {
                Logger.LogInformation("Asking another client for a roll. PlayerId={PlayerId}, RollId={RollId}, UnitId={UnitId}", character.Owner.Id, request.RollId, request.UnitId);
                var rollFromAnotherClient = RetrieveRoll(request);
                Send(playerId, rollFromAnotherClient);
                return;
            }

            await SendLocalRollAsync(playerId, request);
        }

        private void OnNotifyCharacterMove(long playerId, NotifyCharacterMove move)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, Destination={Destination}", nameof(NotifyCharacterMove), playerId, move.UnitId, move.Destination);

            var destination = Mapper.Map<NetworkVector3>(move.Destination);
            GameInteraction.MoveNonCombatCharacter(move.UnitId, destination, move.Delay, move.Orientation);

            Logger.LogInformation("Resending {MessageType}", nameof(NotifyCharacterMove));
            _networkServer.SendAllExcept(playerId, move);
        }

        private void OnPlayerSaveGameSyncChanged(long playerId, PlayerSaveGameSyncChanged changed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, SyncStatus={SyncStatus}", nameof(PlayerSaveGameSyncChanged), playerId, changed.IsSynced);
            lock (ActionLock)
            {
                var player = GetPlayer(playerId);
                if (player == null)
                {
                    Logger.LogError("Player is missing. Game won't start. PlayerId={PlayerId}", playerId);
                    return;
                }

                player.IsSyncedToStartGame = changed.IsSynced;
            }

            TryStartGame();
        }

        private void OnPlayerReadyStatusChanged(long playerId, PlayerReadyStatusChanged readyStatusChanged)
        {
            lock (ActionLock)
            {
                var existingPlayer = GetPlayer(playerId);
                if (existingPlayer == null)
                {
                    Logger.LogWarning("Can't find existing player. PlayerId={PlayerId}", playerId);
                    return;
                }

                existingPlayer.IsReady = readyStatusChanged.IsReady;

                OnPlayersChanged?.Invoke(Game.Players);
                Logger.LogInformation("Sending ready status changed. PlayerId={PlayerId}, IsReady={IsReady}", playerId, existingPlayer.IsReady);
                _networkServer.SendAllExcept(playerId, readyStatusChanged);
            }
        }

        private void OnPlayerNameResponse(long playerId, ClientGameServerConnectionConfirmed response)
        {
            try
            {
                Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Name={Name}", nameof(ClientGameServerConnectionConfirmed), playerId, response?.PlayerName);
                lock (ActionLock)
                {
                    var existingPlayer = GetPlayer(playerId);
                    if (existingPlayer == null)
                    {
                        Logger.LogWarning("Can't process player name update because player doesn't exist. PlayerId={playPlayerIderId}, Name={Name}", playerId, response?.PlayerName);
                        return;
                    }

                    if (string.IsNullOrEmpty(response.PlayerName))
                    {
                        Logger.LogWarning("Can't process player name update because player name is missing. PlayerId={PlayerId}, Name={Name}", playerId, response?.PlayerName);
                        return;
                    }

                    existingPlayer.Name = response.PlayerName;

                    OnPlayersChanged?.Invoke(Game.Players);

                    var players = Game.Players.Select(x => new Networking.Messages.Contracts.NetworkPlayer { Id = x.Id, Name = x.Name, IsReady = x.IsReady }).ToList();
                    var playersChanged = new NotifyPlayersChanged { Players = players };
                    Logger.LogInformation("Sending {MessageType} to ALL players", nameof(NotifyPlayersChanged));
                    _networkServer.SendAll(playersChanged);

                    var notifyGameCharactersChanged = CreateNotifyGameCharactersChanged();
                    Logger.LogInformation("Sending {MessageType} to new player. PlayerId={PlayerId}", nameof(NotifyGameCharactersChanged), playerId);
                    _networkServer.Send(playerId, notifyGameCharactersChanged);

                    var charactersOwnerChanged = CreateNotifyCharactersOwnerChanged();
                    Logger.LogInformation("Sending {MessageType} to new player. PlayerId={PlayerId}", nameof(NotifyCharactersOwnerChanged), playerId);
                    _networkServer.Send(playerId, charactersOwnerChanged);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to handle player name response");
                throw;
            }
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
                Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, Settings={Settings}", nameof(GameServerConnectionSucceeded), playerId, settings);
                var message = new GameServerConnectionSucceeded
                {
                    ClientPlayerId = playerId,
                    GameSettings = Mapper.Map<Networking.Messages.Contracts.NetworkGameSettings>(settings),
                    RestBanterSeed = Game.RestBanterSeed
                };
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
                var message = new NotifyPlayerDisconnected { PlayerId = playerId };
                _networkServer.SendAllExcept(playerId, message);
                ShowPlayerDisconnectedMessage(removedPlayer);

                UpdateStartRestButton();
                UpdateStartRestButtonAfterResults(playerId);
                TryEnableDialogContinueButton();
            }
        }

        private void OnServerStarted(EndPoint endpoint)
        {
            var hostPlayer = new NetworkPlayer(LocalHostPlayerId)
            {
                Name = SettingsProvider.Settings.PlayerName
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

            var settings = GetEnforcedGameSettings();
            GameInteraction.ApplyGameSettings(settings);

            OnConnected?.Invoke(Game.Connectivity);
            OnPlayersChanged?.Invoke(Game.Players);
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

        private NotifyGameCharactersChanged CreateNotifyGameCharactersChanged()
        {
            var message = new NotifyGameCharactersChanged
            {
                Characters = Mapper.Map<List<Networking.Messages.Contracts.NetworkCharacterOwnership>>(Game.Characters)
            };
            return message;
        }
    }
}
