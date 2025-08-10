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
            IUniqueIdGenerator uniqueIdGenerator,
            IMapper mapper)
            : base(logger,
                  mapper,
                  multiplayerSettingsProvider,
                  gameInteractionService,
                  diceRollStorage,
                  fileSystemService,
                  uniqueIdGenerator)
        {
            _networkServer = networkServer;
        }

        public void Create(string saveFilePath, string gameId, List<NetworkCharacterOwnership> characters)
        {
            if (_networkServer.IsActive)
            {
                _networkServer.Dispose();
            }

            RegisterHandlers();

            Game?.Reset();

            Game = new NetworkGame(saveFilePath)
            {
                LocalPlayerId = LocalHostPlayerId,
                Id = gameId,
            };

            Game.Characters.AddRange(characters);

            _networkServer.Start(SettingsProvider.Settings.HostPortRangeStart, SettingsProvider.Settings.HostPortRangeEnd);

            Logger.LogInformation("Host has been created. SavePath={savePath}, Portraits={portraits}", saveFilePath, string.Join(";", Game.Characters.Select(c => c.Portrait)));
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

            Logger.LogInformation("Notifying game characters changed. Portraits={portraits}", string.Join(";", Game.Characters.Select(c => c.Portrait)));
            var message = CreateNotifyGameCharactersChanged();
            _networkServer.SendAll(message);
        }

        public void ChangeCharacterOwner(int characterIndex, int playerIndex)
        {
            lock (ActionLock)
            {
                if (Game.Players.Count < playerIndex)
                {
                    Logger.LogError("Unable to change character owner as playerIndex is out of range. PlayersCount={playersCount}, PlayerIndex={playerIndex}", Game.Players.Count, playerIndex);
                    return;
                }

                var player = Game.Players[playerIndex];

                if (Game.Characters.Count < characterIndex)
                {
                    Logger.LogError("Unable to change character owner as characterIndex is out of range. CharacterOwnersCount={characterOwnersCount}, CharacterIndex={characterIndex}", Game.Characters.Count, characterIndex);
                    return;
                }

                var character = Game.Characters[characterIndex];
                if (character.Owner == player)
                {
                    return;
                }

                character.Owner = player;
                Logger.LogInformation("New character owner. CharacterName={characterName}, PlayerId={playerId}, PlayerName={playerName}", character.Name, player.Id, player.Name);

                var charactersOwnerChanged = CreateNotifyCharactersOwnerChanged();
                _networkServer.SendAll(charactersOwnerChanged);
            }
        }

        public void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation)
        {
            if (Game.Combat != null)
            {
                return;
            }

            Logger.LogInformation("Sending {messageType}. UnitId={unitId}, Destination={destination}, Delay={delay}, Orientation={orientation}", nameof(NotifyCharacterMove), unitId, destination, delay, orientation);
            var message = new NotifyCharacterMove
            {
                UnitId = unitId,
                Destination = new Networking.Messages.Contracts.NetworkVector3(destination.X, destination.Y, destination.Z),
                Delay = delay,
                Orientation = orientation
            };
            _networkServer.SendAll(message);
        }

        public void Dispose()
        {
            Logger.LogInformation("Dispose");

            lock (ActionLock)
            {
                Game?.Reset();
            }

            _networkServer.Dispose();
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
                Logger.LogError("Unable to start a game due to missing save file. Path={savePath}", Game.SaveFilePath);
                return;
            }

            Game.Stage = NetworkGameStage.Initializing;
            var gameStageChanged = new NotifyGameStageChanged { Stage = Game.Stage.ToString() };
            _networkServer.SendAll(gameStageChanged);

            lock (ActionLock)
            {
                var saveGameMessageAssigned = new NotifySaveGameAssigned { GameId = Game.Id, Content = content, IsForceLoad = false };
                Logger.LogInformation("Sending save game file content to all players. Size={saveFileSize}", saveGameMessageAssigned.Content.Length);
                _networkServer.SendAll(saveGameMessageAssigned);
                Game.Stage = NetworkGameStage.WaitingForPlayersInitialization;
                Logger.LogInformation("Waiting for players to confirm delivery. GameStatus={gameStatus}", Game.Stage);
                GetHost().IsSyncedToStartGame = true;
            }

            TryStartGame();
        }

        public override void OnAreaScenesLoaded()
        {
            base.OnAreaScenesLoaded();

            lock (ActionLock)
            {
                EnsureForcePaused(UIStringConsts.GameNotifications.ForcedPauseReasons.AreaLoading);
                var localPlayerId = GetLocalPlayerId();
                Game.ForcedPause.ReadyPlayers.Add(localPlayerId);
            }

            GameInteraction.Pause(true);

            TryEndForcedPause();
        }

        public void LeaveArea(string areaExitId)
        {
            Logger.LogInformation("Sending {messageType}. AreaExitId={areaExitId}", nameof(NotifyPartyLeaveArea), areaExitId);
            var message = new NotifyPartyLeaveArea { AreaExitId = areaExitId };
            _networkServer.SendAll(message);
        }

        public void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer)
        {
            Logger.LogInformation("Showing dialog Cue. DialogName={dialogName}, CueName={cueName}, HasSystemAnswer={hasSystemAnswer}", dialogName, cueName, hasSystemAnswer);
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
            Logger.LogInformation("Select Dialog Answer. DialogName={dialogName}, CueName={cueName} Answer={answer}, IsExitAnswer={isExitAnswer}, ManualUnitSelectionId={unitId}", dialogName, cueName, answerName, isExitAnswer, manualUnitSelectionId);

            var missingPlayers = GetPlayersWhoHaveNotSeenCueYet(cueName);
            if (missingPlayers.Count > 0)
            {
                Logger.LogWarning("Some players haven't seen the dialog yet. Players={playerNames}", string.Join(";", missingPlayers.Select(p => p.Name)));
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
                Logger.LogWarning("Answer is not set, most likely it's a first dialog cue or cutscene intermission. DialogName={dialogName}", Game.Dialog.Name);
                return;
            }

            Logger.LogInformation("Sending selected answer to clients. DialogName={dialogName}, CueName={cueName}, AnswerName={answerName}, ManualUnitSelectionId={unitId}", Game.Dialog.Name, Game.Dialog.Answer.CueName, Game.Dialog.Answer.AnswerName, Game.Dialog.Answer.ManualUnitSelectionId);

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
            Logger.LogInformation("Sending dialog started to all clients. DialogName={dialogName}, TargetUnitId={targetUnitId}, InitiatorUnitId={initiatorUnitId}, MapObjectId={mapObjectId}, SpeakerKey={speakerKey}",
                message.DialogName, message.TargetUnitId, message.InitiatorUnitId, message.MapObjectId, message.SpeakerKey);

            Send(message);

            if (!string.Equals(Game.Dialog?.Name, dialogName, StringComparison.OrdinalIgnoreCase))
            {
                Game.Dialog = new NetworkDialog(dialogName);
            }

            return true;
        }

        public bool CanInitializeCombat()
        {
            // confirmation from host is required
            if (Game.Combat == null)
            {
                return true;
            }

            PrepareCombat();

            return true;
        }

        public bool CanContinueCombat()
        {
            if (Game.Combat == null || !Game.Combat.IsCombatPrepared)
            {
                return false;
            }

            if (Game.Combat.Round == 1 && !Game.Combat.IsInitialized)
            {
                var unitsInCombat = GameInteraction.GetUnitsInCombat();
                var unitsCombatOrder = GameInteraction.GetUnitsCombatOrder();
                var nextUnitTurn = GameInteraction.GetNextUnitTurn();
                var message = new NotifyCombatInitialized
                {
                    Units = Mapper.Map<List<Networking.Messages.Contracts.NetworkUnit>>(unitsInCombat),
                    UnitsCombatOrder = unitsCombatOrder,
                    NextUnitTurn = nextUnitTurn
                };
                _networkServer.SendAll(message);
                Game.Combat.IsInitialized = true;
                Game.Combat.PlayersCombatInitialization.TryAdd(Game.LocalPlayerId, true);
                Logger.LogInformation("Sending {messageType}. UnitsInCombat={unitsCount}, CombatOrder={order}", nameof(NotifyCombatInitialized), message.Units.Count, message.UnitsCombatOrder);
            }

            var canContinue = Game.Combat.PlayersCombatInitialization.Count >= Game.Players.Count;
            return canContinue;
        }

        public bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound)
        {
            try
            {
                return OnTurnStart(unitId, actingInSurpriseRound);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to handle {methodName}. UnitId={unitId}, ActingInSurpriseRound={actingInSurpriseRound}", nameof(OnBeforeStartTurn), unitId, actingInSurpriseRound);
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
                Logger.LogError(ex, "Unable to process {methodName}. UnitId={unitId}", nameof(OnBeforeEndTurn), unitId);
                throw;
            }
        }

        public bool IsDiceRollOwner(bool silent)
        {
            return IsRolledByHost(silent) || IsRolledByLocalPlayer(silent);
        }

        public void OnPerceptionCheck(NetworkPerceptionCheck check)
        {
            Logger.LogInformation("Sending {messageType}. UnitId={unitID}, MapObjectId={round}, Result={result}", nameof(NotifyPerceptionCheckRolled), check.UnitId, check.MapObject.Id);
            var message = new NotifyPerceptionCheckRolled
            {
                Check = Mapper.Map<Networking.Messages.Contracts.NetworkPerceptionCheck>(check)
            };

            Send(message);
        }

        public void OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check)
        {
            Logger.LogInformation("Sending {messageType}. TargetUnitId={targetUnitId}, InitiatorUnitId={initiatorId}, StatType={statType}, DC={dc}", nameof(NotifyInspectionKnowledgeCheckRolled), check.TargetUnitId, check.InitiatorUnitId, check.StatType, check.DC);
            var message = new NotifyInspectionKnowledgeCheckRolled
            {
                Check = Mapper.Map<Networking.Messages.Contracts.NetworkInspectionKnowledgeCheck>(check)
            };

            Send(message);
        }

        public bool OnSpawnCampPlace(NetworkVector3 position)
        {
            Logger.LogInformation("Sending spawn camp event. Position={position}", position);
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
            Logger.LogInformation("Sending {messageType}. IsOn={isOn}", nameof(NotifyCampingUseHealingSpellsChanged), isOn);
            Send(message);
        }

        public void OnCampingStateChanged(NetworkCampingState state)
        {
            var message = new NotifyCampingStateChanged
            {
                State = Mapper.Map<Networking.Messages.Contracts.NetworkCampingState>(state)
            };

            Logger.LogInformation("Sending {messageType}.CookingBlueprintRecipeId={cookingId}, PotionBlueprintRecipeId={potionId}, ScrollBlueprintRecipeId={ScrollId}, IterationsCount={iterations}, AutotuneIterations={autotuneIterations}", nameof(NotifyCampingStateChanged),
                message.State.CookingBlueprintRecipeId, message.State.PotionBlueprintRecipeId, message.State.ScrollBlueprintRecipeId, message.State.IterationsCount, message.State.AutotuneIterationsStatus);

            Send(message);
        }

        public void OnCampingUnitsRoleChanged(List<NetworkCampingRole> roles)
        {
            var rolesData = string.Join(" ,", roles.Select(r => $"[RoleType={r.RoleType} PrimaryUnit={r.PrimaryUnitId} SecondaryUnit={r.SecondaryUnitId}]"));
            Logger.LogInformation("Sending {messageType}. RolesCount={rolesCount}, RolesData={rolesData}", nameof(NotifyCampingStateChanged), roles.Count, rolesData);

            var message = new NotifyCampingUnitsRoleChanged
            {
                Roles = Mapper.Map<List<Networking.Messages.Contracts.NetworkCampingRole>>(roles),
            };
            Send(message);
        }

        public void OnStartRest()
        {
            Logger.LogInformation("Sending {messageType}", nameof(NotifyRestStarted));
            var message = new NotifyRestStarted();
            Send(message);
            Game.RandomEncounter = null;
            Game.PlayersFinishedRest.Clear();
        }

        public bool OnShowRestView(RestPhase phase)
        {
            Logger.LogInformation("Showing rest view. Phase={phase}", phase);
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

                Game.RandomEncounter = Mapper.Map<NetworkRandomEncounter>(encounterContext.Recording);

                Logger.LogInformation("Random encounter context has been stored. Data={encounter}", Game.RandomEncounter);

                if (Game.RandomEncounter.RandomUnitSeed.HasValue)
                {
                    EnsureForcePaused(UIStringConsts.GameNotifications.ForcedPauseReasons.RandomEncounterLoading, SettingsProvider.Settings.ForcedPauseRandomEncounterTerminationDelay);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to store random encounter context");
                throw;
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

        protected override DiceRollValueResponse RetrieveRoll(DiceRollValueRequest request, string unitId)
        {
            var character = GetCharacterOwnership(unitId);
            if (character?.Owner == null)
            {
                Logger.LogError("Unable to retrieve roll due to missing character ownership. UnitId={unitId}", unitId);
                return null;
            }

            if (character.Owner.Id == LocalHostPlayerId)
            {
                Logger.LogError("Host is character owner, but tries to retrieve network roll. UnitId={unitId}", unitId);
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

            AddPlayerReadyStatus(PlayerTurnReadinessType.Start, Game.LocalPlayerId, Game.Combat.Round, Game.Combat.Turn.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitSynchronization, Game.LocalPlayerId, Game.Combat.Round, Game.Combat.Turn.UnitId);

            TryStartTurn();
        }

        protected override void OnTurnStartConfirmed()
        {
            var message = new NotifyCombatTurnStarted
            {
                Round = Game.Combat.Round,
                UnitId = Game.Combat.Turn.UnitId
            };
            _networkServer.SendAll(message);
        }

        protected void TryStartTurn()
        {
            Logger.LogInformation("Checking if turn could be started. Round={round}, UnitId={unitId}", Game.Combat.Round, Game.Combat.Turn.UnitId);

            var turnReadinessKey = GetTurnReadinessKey(Game.Combat.Round, Game.Combat.Turn.UnitId);
            var notInitializedPlayers = GetMissingPlayers(turnReadinessKey, Game.Combat.PlayersTurnStartInitialization);
            if (notInitializedPlayers.Count > 0)
            {
                Logger.LogInformation("Unable to start turn due to missing players turn initialization. MissingPlayers={players}", string.Join(";", notInitializedPlayers.Select(p => p.Name)));
                return;
            }

            lock (ActionLock)
            {
                if (Game.Combat.Turn.RequiresTurnEntitiesSynchronization)
                {
                    Game.Combat.Turn.RequiresTurnEntitiesSynchronization = false;
                    var unitsToSync = GameInteraction.GetUnitsInCombat();
                    var message = new NotifyCombatTurnSynchronizationRequired
                    {
                        Units = Mapper.Map<List<Networking.Messages.Contracts.NetworkUnit>>(unitsToSync)
                    };
                    _networkServer.SendAll(message);
                }
            }

            var notSynchronizedPlayers = GetMissingPlayers(turnReadinessKey, Game.Combat.PlayersTurnSynchronization);
            if (notSynchronizedPlayers.Count > 0)
            {
                Logger.LogInformation("Unable to start turn due to missing players turn synchronization. MissingPlayers={players}", string.Join(";", notSynchronizedPlayers.Select(p => p.Name)));
                return;
            }

            OnTurnStartConfirmed();

            Game.Combat.Turn.IsInProgress = true;
            GameInteraction.StartTurnBasedCombatTurn(Game.Combat.Turn.IsActingInSurpriseRound);
        }

        protected override void OnLocalPlayerTurnEnded()
        {
            var message = new PlayerCombatTurnEnded { Round = Game.Combat.Round, UnitId = Game.Combat.Turn.UnitId };
            _networkServer.SendAll(message);
        }

        private void AddCueWitness(string cueName, long playerId)
        {
            if (Game.Dialog == null)
            {
                Logger.LogError("Trying to add witness to null dialog. CueName={cueName}, PlayerId={playerId}", cueName, playerId);
                return;
            }

            Game.Dialog.CueViews.AddOrUpdate(cueName, (key) => new HashSet<long>([playerId]), (key, existing) =>
            {
                existing.Add(playerId);
                return existing;
            });

            Logger.LogInformation("Cue witness has been added. CueName={cueName}, PlayerId={playerId}", cueName, playerId);
        }

        private List<NetworkPlayer> GetPlayersWhoHaveNotSeenCueYet(string cueName)
        {
            if (Game.Dialog == null)
            {
                Logger.LogWarning("Trying to get cue players, but dialog is null. CueName={cueName}", cueName);
                return [];
            }

            if (!Game.Dialog.CueViews.TryGetValue(cueName, out var cueViews))
            {
                Logger.LogWarning("Specified cue doesn't exist in the views history. CueName={cueName}", cueName);
                return [];
            }

            var players = Game.Players.Where(p => !cueViews.Contains(p.Id)).ToList();
            return players;
        }

        private void TryEnableDialogContinueButton()
        {
            if (Game.Dialog == null)
            {
                Logger.LogError("Unable to enable continue button because current dialog is null");
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
                Logger.LogInformation("Cannot proceed with dialog yet. CurrentCue={currentCue}, MissingPlayers={missingPlayers}", currentCue, string.Join(";", missingPlayers.Select(x => x.Name)));
                return;
            }

            Logger.LogInformation("All players have witnessed current cue. CueName={cueName}", currentCue);
            GameInteraction.SetDialogContinueButtonState(true);
        }

        private void TryEndForcedPause()
        {
            try
            {
                Logger.LogInformation("Checking if forced pause could be removed. PauseIsNull={isNull}", Game.ForcedPause == null);

                if (Game.ForcedPause == null)
                {
                    return;
                }

                lock (ActionLock)
                {
                    var allReady = Game.ForcedPause.ReadyPlayers.Count >= Game.Players.Count;
                    if (!allReady)
                    {
                        Logger.LogInformation("Not everyone is ready, forced pause will remain. ReadyPlayers={readyPlayers}", Game.ForcedPause.ReadyPlayers);
                        return;
                    }

                    Game.Stage = NetworkGameStage.Playing;
                    var removalDelay = Game.ForcedPause.RemovalDelay;
                    var delay = removalDelay.HasValue ? Task.Delay(removalDelay.Value) : Task.CompletedTask;
                    Game.ForcedPause = null;

                    Logger.LogInformation("Forced pause is going to be ended. Delay={delay}", removalDelay);
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
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}", nameof(ClientAreaLoaded), playerId);
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
                // this is special case when client sends notify as usually all notifies are sent by host only
                // we need to load game ASAP on both host/remaining clients
                .Register<NotifySaveGameAssigned>(OnNotifySaveGameAssigned)
                .Register<NotifyUnitClicked>(OnNotifyUnitClicked)
                .Register<NotifyGroundClicked>(OnNotifyGroundClicked)
                .Register<NotifyMapObjectClicked>(OnNotifyMapObjectClicked)

                // this is kinda special because requester is blocking the game loop thread until <see cref="DiceRollValueResponse"/> is received
                .Register<DiceRollValueRequest>(OnDiceRollValueRequest)
                .Register<DiceRollValueResponse>(null) // usable as awaiter only
                .Register<RandomEncounterContextRequest>(OnRandomEncounterContextRequest)

                .Register<PlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
                .Register<ClientGameServerConnectionConfirmed>(OnPlayerNameResponse)
                .Register<PlayerSaveGameSyncChanged>(OnPlayerSaveGameSyncChanged)
                .Register<CharacterMove>(OnCharacterMove)
                .Register<ClientAreaLoaded>(OnClientAreaLoaded)
                .Register<CueWitnessed>(OnCueWitnessed)
                .Register<DialogCueAnswerSuggested>(OnDialogCueAnswerSuggested)
                .Register<ClientDialogStartRequested>(OnClientDialogStartRequested)
                .Register<NotifyAbilityUse>(OnNotifyAbilityUsed)
                .Register<NotifyToggleActivatableAbility>(OnNotifyToggleActivatableAbility)
                .Register<PlayerCombatTurnEnded>(OnPlayerCombatTurnEnded)
                .Register<ClientCombatInitialized>(OnClientCombatInitialized)
                .Register<ClientCombatTurnStarted>(OnClientCombatTurnStarted)
                .Register<ClientCombatTurnSynchronized>(OnClientCombatTurnSynchronized)
                .Register<NotifyContainerLooted>(OnNotifyContainerLooted)
                .Register<NotifyDropItem>(OnNotifyDropItem)
                .Register<NotifyEquipmentSlotChanged>(OnNotifyEquipmentSlotChanged)
                .Register<NotifyActiveHandEquipmentSetChanged>(OnNotifyActiveHandEquipmentSetChanged)
                .Register<NotifyOvertipInteracted>(OnNotifyOvertipInteracted)
                .Register<NotifyUnitJoinedMidCombat>(OnNotifyUnitJoinedMidCombat)
                .Register<ClientGameModeTypeStarted>(OnClientGameModeTypeStarted)
                .Register<ClientGameModeTypeEnded>(OnClientGameModeTypeEnded)
                .Register<ClientRestEnded>(OnClientRestEnded)
                ;
        }

        private async void OnRandomEncounterContextRequest(long playerId, RandomEncounterContextRequest request)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}", nameof(RandomEncounterContextRequest));

            var timeout = Task.Delay(request.Timeout);
            while (!timeout.IsCompleted && Game.RandomEncounter == null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }

            var response = new RandomEncounterContextResponse
            {
                Context = Mapper.Map<Networking.Messages.Contracts.NetworkRandomEncounter>(Game.RandomEncounter)
            };

            Logger.LogInformation("Sending {messageType}. IsAvailable={isAvailable}", response.Context != null);

            Send(playerId, response);
        }

        private void OnClientRestEnded(long playerId, ClientRestEnded ended)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}", nameof(ClientRestEnded));

            UpdateStartRestButtonAfterResults(playerId);
        }

        private void OnClientGameModeTypeEnded(long playerId, ClientGameModeTypeEnded ended)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, TypeId={typeId}", nameof(ClientGameModeTypeEnded), playerId, ended.TypeId);
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
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, TypeId={typeId}", nameof(ClientGameModeTypeStarted), playerId, started.TypeId);
            var gameMode = GameModeType.All.FirstOrDefault(g => g.Index == started.TypeId);
            RegisterGameMode(gameMode, playerId);
            if (gameMode == GameModeType.Rest)
            {
                UpdateStartRestButton();
            }
        }

        private void OnNotifyUnitJoinedMidCombat(long playerId, NotifyUnitJoinedMidCombat combat)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, UnitId={unitId}", nameof(NotifyUnitJoinedMidCombat), playerId, combat.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitJoinedMidCombat, combat.PlayerId, combat.UnitId);

            Logger.LogInformation("Resending {messageType}", nameof(NotifyUnitJoinedMidCombat));
            _networkServer.SendAllExcept(playerId, combat);
        }

        private void OnNotifyOvertipInteracted(long playerId, NotifyOvertipInteracted interacted)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, MapObjectId={mapObjectId}, UnitsCount={unitsCount}", nameof(NotifyOvertipInteracted), playerId, interacted.Overtip.MapObject.Id, interacted.Overtip.Units);
            var overtip = Mapper.Map<NetworkOvertip>(interacted.Overtip);
            GameInteraction.InteractWithOvertip(overtip);

            Logger.LogInformation("Resending {messageType}", nameof(NotifyOvertipInteracted));
            _networkServer.SendAllExcept(playerId, interacted);
        }

        private void OnNotifyActiveHandEquipmentSetChanged(long playerId, NotifyActiveHandEquipmentSetChanged changed)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, UnitId={unitId}, SetIndex={setIndex}", nameof(NotifyEquipmentSlotChanged), playerId, changed.Set.UnitId, changed.Set.Index);
            var set = Mapper.Map<NetworkActiveHandEquipmentSet>(changed.Set);
            GameInteraction.SetActiveHandEquipmentSet(set);

            Logger.LogInformation("Resending {messageType}", nameof(NotifyActiveHandEquipmentSetChanged));
            _networkServer.SendAllExcept(playerId, changed);
        }

        private void OnNotifyEquipmentSlotChanged(long playerId, NotifyEquipmentSlotChanged slotChanged)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, SlotType={slotType}, SlotIndex={slotIndex}, ItemId={itemId}, OwnerId={ownerId}", nameof(NotifyEquipmentSlotChanged), playerId, slotChanged.Slot.Position.Type, slotChanged.Slot.Position.Index, slotChanged.Slot.Item?.UniqueId, slotChanged.Slot.OwnerId);
            var slot = Mapper.Map<NetworkEquipmentSlot>(slotChanged.Slot);
            GameInteraction.UpdateEquipmentSlot(slot);

            Logger.LogInformation("Resending {messageType}", nameof(NetworkEquipmentSlot));
            _networkServer.SendAllExcept(playerId, slotChanged);
        }

        private void OnNotifyDropItem(long playerId, NotifyDropItem item)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, OwnerId={ownerId}, ItemId={itemId}, ItemName={itemName}", nameof(NotifyDropItem), playerId, item.Drop.OwnerEntityId, item.Drop.Item.UniqueId, item.Drop.Item.Name);

            var dropItem = Mapper.Map<NetworkDropItem>(item.Drop);
            GameInteraction.DropItem(dropItem);

            Logger.LogInformation("Resending {messageType}", nameof(NotifyDropItem));
            _networkServer.SendAllExcept(playerId, item);
        }

        private void OnNotifyContainerLooted(long playerId, NotifyContainerLooted looted)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, ContainerId={containerId}, ContainerPosition={containerPosition}, ItemsCount={itemsCount}, Items={itemsIds}",
               nameof(NotifyContainerLooted), playerId, looted.Container.Id, looted.Container.Position, looted.Container.Items.Count, looted.Container.Items.Select(i => i.UniqueId));

            var container = Mapper.Map<NetworkLootContainer>(looted.Container);
            GameInteraction.CollectContainerLoot(container);

            Logger.LogInformation("Resending {messageType}", nameof(NotifyContainerLooted));
            _networkServer.SendAllExcept(playerId, looted);
        }

        private void OnNotifyToggleActivatableAbility(long playerId, NotifyToggleActivatableAbility activatableAbility)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, AbilityId={abilityId}, IsActive={isActive}", nameof(NotifyToggleActivatableAbility), playerId, activatableAbility.Ability.Id, activatableAbility.Ability.IsActive);

            var ability = Mapper.Map<NetworkActivatableAbility>(activatableAbility.Ability);
            GameInteraction.ToggleActivatableAbility(ability);

            Logger.LogInformation("Resending {messageType}", nameof(NotifyToggleActivatableAbility));
            _networkServer.SendAllExcept(playerId, activatableAbility);
        }

        private void OnNotifyAbilityUsed(long playerId, NotifyAbilityUse abilityUse)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, AbilityId={abilityId}", nameof(NotifyAbilityUse), playerId, abilityUse.Ability.Id);

            var ability = Mapper.Map<NetworkAbility>(abilityUse.Ability);
            GameInteraction.UseAbility(ability);

            Logger.LogInformation("Resending {messageType}", nameof(NotifyAbilityUse));
            _networkServer.SendAllExcept(playerId, abilityUse);
        }

        private void OnClientCombatTurnSynchronized(long playerId, ClientCombatTurnSynchronized synchronized)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, Round={round}, UnitId={unitId}", nameof(ClientCombatTurnSynchronized), playerId, synchronized.Round, synchronized.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitSynchronization, playerId, synchronized.Round, synchronized.UnitId);
            TryStartTurn();
        }

        private void OnPlayerCombatTurnEnded(long playerId, PlayerCombatTurnEnded ended)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, Round={round}, UnitId={unitId}", nameof(PlayerCombatTurnEnded), playerId, ended.Round, ended.UnitId);

            _networkServer.SendAllExcept(playerId, ended);

            if (Game.Combat.Round != ended.Round && !string.Equals(Game.Combat.Turn?.UnitId, ended.UnitId, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning("Player ended invalid turn. PlayerId={playerId}, PlayerRound={round}, PlayerUnitId={unitId}, LocalRound={localRound}, LocalUnitId={localUnitId}", playerId, ended.Round, ended.UnitId, Game.Combat.Round, Game.Combat.Turn?.UnitId);
                return;
            }

            EndLocalTurn();
        }

        private void OnClientCombatTurnStarted(long playerId, ClientCombatTurnStarted started)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, Round={round}, UnitId={unitId}", nameof(ClientCombatTurnStarted), playerId, started.Round, started.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.Start, playerId, started.Round, started.UnitId);

            // player turn could be started earlier than host so recording readiness is enough
            if (started.Round != Game.Combat.Round || !string.Equals(started.UnitId, Game.Combat.Turn?.UnitId, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning("Client has started different turn. Round={round}, UnitId={unitId}, ClientRound={clientRound}, ClientUnitId={clientUnitId}", Game.Combat.Round, Game.Combat.Turn?.UnitId, started.Round, started.UnitId);
                return;
            }

            TryStartTurn();
        }

        private void OnNotifyGroundClicked(long playerId, NotifyGroundClicked clicked)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, SelectedUnitId={selectedUnits}, WorldPosition={worldPosition}", nameof(NotifyGroundClicked), playerId, clicked.Click.SelectedUnits.Count, clicked.Click.WorldPosition);
            if (Game.Combat == null)
            {
                Logger.LogWarning($"{nameof(NotifyGroundClicked)} is ignored out of combat");
                return;
            }

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickGroundInCombat(click);

            Logger.LogInformation("Resending {messageType} to other players", nameof(NotifyGroundClicked));
            _networkServer.SendAllExcept(playerId, click);
        }

        private void OnNotifyUnitClicked(long playerId, NotifyUnitClicked clicked)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, TargetUnitId={targetUnitId}, SelectedUnits={selectedUnits}", nameof(NotifyUnitClicked), playerId, clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickUnit(click);

            Logger.LogInformation("Resending {messageType} to other players", nameof(NotifyUnitClicked));
            _networkServer.SendAllExcept(playerId, click);
        }

        private void OnNotifyMapObjectClicked(long playerId, NotifyMapObjectClicked clicked)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, TargetUnitId={targetUnitId}, SelectedUnits={selectedUnits}", nameof(NotifyMapObjectClicked), playerId, clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickMapObject(click);

            Logger.LogInformation("Resending {messageType} to other players", nameof(NotifyMapObjectClicked));
            _networkServer.SendAllExcept(playerId, clicked);
        }

        private void OnNotifySaveGameAssigned(long playerId, NotifySaveGameAssigned assigned)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, IsForceLoad={isForceLoad}, SaveGameSize={saveGameSize}", nameof(NotifySaveGameAssigned), playerId, assigned.IsForceLoad, assigned.Content.Length);

            Logger.LogInformation("Resending {messageType} to other players", nameof(NotifySaveGameAssigned));
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
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}", nameof(ClientCombatInitialized), playerId);
            if (Game.Combat == null)
            {
                Logger.LogWarning("Received client initialization, but combat is null. PlayerId={playerId}", playerId);
                return;
            }

            if (!Game.Combat.PlayersCombatInitialization.TryAdd(playerId, true))
            {
                Logger.LogWarning("Received duplicate client initialization. PlayerId={playerId}", playerId);
            }
        }

        private async void OnClientDialogStartRequested(long playerId, ClientDialogStartRequested requested)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, DialogName={dialogName}, TargetUnitId={targetUnitId}, InitiatorUnitId={initiatorUnitId}, MapObjectId={mapObjectId}, SpeakerKey={speakerKey}",
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
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, DialogName={dialogName}, CueName={cueName}, AnswerName={answerName}", nameof(DialogCueAnswerSuggested), playerId, suggested.DialogName, suggested.CueName, suggested.AnswerName);

            if (Game.Dialog == null)
            {
                Logger.LogError("Received dialog answer suggestion, but there is no active dialog right now. SuggestedDialogName={suggestedDialogName}, SuggestedCueName={suggestedCueName}, SuggestedAnswer={suggestedAnswerName}", suggested.DialogName, suggested.CueName, suggested.AnswerName);
                return;
            }

            if (!string.Equals(Game.Dialog.Name, suggested.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog suggestion has mismatched dialog name. SuggestedDialogName={suggestedDialogName}, CurrentDialogName={currentCueName}", suggested.DialogName, Game.Dialog.Name);
                return;
            }

            if (!string.Equals(Game.Dialog.CurrentCueName, suggested.CueName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Dialog suggestion has mismatched cue name. SuggestedCueName={suggestedCueName}, CurrentCueName={currentCueName}", suggested.CueName, Game.Dialog.CurrentCueName);
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
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, DialogName={dialogName}, CueName={cueName}", nameof(CueWitnessed), playerId, witnessed.DialogName, witnessed.CueName);
            if (Game.Dialog == null)
            {
                Logger.LogError("Received cue witness, but there is no active dialog right now. WitnessedDialogName={witnessedDialogName}, WitnessedCueName={witnessedCueName}", witnessed.DialogName, witnessed.CueName);
                return;
            }

            if (!string.Equals(Game.Dialog.Name, witnessed.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("Cue witness has mismatched dialog. WitnessedDialogName={witnessedDialogName}, CurrentDialogName={currentCueName}", witnessed.DialogName, Game.Dialog.Name);
                return;
            }

            AddCueWitness(witnessed.CueName, playerId);
            TryEnableDialogContinueButton();
        }

        private async void OnDiceRollValueRequest(long playerId, DiceRollValueRequest request)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, RollId={rollId}, UnitId={unitId}", nameof(DiceRollValueRequest), playerId, request.RollId, request.UnitId);

            var character = GetCharacterOwnership(request.UnitId);
            var isAI = GameInteraction.IsUnitAI(request.UnitId);
            // so basically in combat we need to ask another player for rolls in case he is the owner of the turn
            if (Game.Combat != null
                && !isAI
                && character?.Owner != null
                && character.Owner.Id != LocalHostPlayerId
                && character.Owner.Id != playerId)
            {
                Logger.LogInformation("Asking another client for a roll. PlayerId={playerId}, RollId={rollId}, UnitId={unitId}", character.Owner.Id, request.RollId, request.UnitId);
                var rollFromAnotherClient = RetrieveRoll(request, request.UnitId);
                Send(playerId, rollFromAnotherClient);
                return;
            }

            await SendLocalRollAsync(playerId, request);
        }

        private void OnCharacterMove(long playerId, CharacterMove move)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, UnitId={unitId}, Destination={destination}", nameof(CharacterMove), playerId, move.UnitId, move.Destination);

            var destination = Mapper.Map<NetworkVector3>(move.Destination);
            GameInteraction.MoveNonCombatCharacter(move.UnitId, destination, move.Delay, move.Orientation);

            var notifyMove = new NotifyCharacterMove
            {
                UnitId = move.UnitId,
                Destination = move.Destination,
                Delay = move.Delay,
                Orientation = move.Orientation
            };
            _networkServer.SendAllExcept(playerId, notifyMove);
        }

        private void OnPlayerSaveGameSyncChanged(long playerId, PlayerSaveGameSyncChanged changed)
        {
            Logger.LogInformation("Received {messageType}. PlayerId={playerId}, SyncStatus={syncStatus}", nameof(PlayerSaveGameSyncChanged), playerId, changed.IsSynced);
            lock (ActionLock)
            {
                var player = GetPlayer(playerId);
                if (player == null)
                {
                    Logger.LogError("Player is missing. Game won't start. Player Id={playerId}", playerId);
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
                    Logger.LogWarning("Can't find existing player. PlayerId={playerId}", playerId);
                    return;
                }

                existingPlayer.IsReady = readyStatusChanged.IsReady;

                OnPlayersChanged?.Invoke(Game.Players);
                Logger.LogInformation("Sending ready status changed. PlayerId={playerId}, IsReady={isReady}", playerId, existingPlayer.IsReady);
                _networkServer.SendAllExcept(playerId, readyStatusChanged);
            }
        }

        private void OnPlayerNameResponse(long playerId, ClientGameServerConnectionConfirmed response)
        {
            try
            {
                Logger.LogInformation("Received {messageType}. PlayerId={playerId}, Name={name}", nameof(ClientGameServerConnectionConfirmed), playerId, response?.PlayerName);
                lock (ActionLock)
                {
                    var existingPlayer = GetPlayer(playerId);
                    if (existingPlayer == null)
                    {
                        Logger.LogWarning("Can't process player name update because player doesn't exist. PlayerId={playerId}, Name={name}", playerId, response?.PlayerName);
                        return;
                    }

                    if (string.IsNullOrEmpty(response.PlayerName))
                    {
                        Logger.LogWarning("Can't process player name update because player name is missing. PlayerId={playerId}, Name={name}", playerId, response?.PlayerName);
                        return;
                    }

                    existingPlayer.Name = response.PlayerName;

                    OnPlayersChanged?.Invoke(Game.Players);

                    var players = Game.Players.Select(x => new Networking.Messages.Contracts.NetworkPlayer { Id = x.Id, Name = x.Name, IsReady = x.IsReady }).ToList();
                    var playersChanged = new NotifyPlayersChanged { Players = players };
                    Logger.LogInformation("Sending {messageType} to ALL players", nameof(NotifyPlayersChanged));
                    _networkServer.SendAll(playersChanged);

                    var notifyGameCharactersChanged = CreateNotifyGameCharactersChanged();
                    Logger.LogInformation("Sending {messageType} to new player. PlayerId={playerId}", nameof(NotifyGameCharactersChanged), playerId);
                    _networkServer.Send(playerId, notifyGameCharactersChanged);

                    var charactersOwnerChanged = CreateNotifyCharactersOwnerChanged();
                    Logger.LogInformation("Sending {messageType} to new player. PlayerId={playerId}", nameof(NotifyCharactersOwnerChanged), playerId);
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
                    Logger.LogWarning("Player already exists. PlayerId={playerId}", playerId);
                    return;
                }

                var player = new NetworkPlayer(playerId);
                Game.Players.Add(player);

                var settings = GameInteraction.GetGameSettings();
                Logger.LogInformation("Sending {messageType}. PlayerId={playerId}, Settings={gameSettings}", nameof(GameServerConnectionSucceeded), playerId, settings);
                var message = new GameServerConnectionSucceeded
                {
                    ClientPlayerId = playerId,
                    GameSettings = Mapper.Map<Networking.Messages.Contracts.NetworkGameSettings>(settings)
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
