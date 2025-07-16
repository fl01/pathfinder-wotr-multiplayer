using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Rolls;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerHost : IMultiplayerHost
    {
        private readonly ILogger<MultiplayerHost> _logger;
        private readonly INetworkServer _networkServer;
        private readonly IFileSystemService _fileSystemService;
        private readonly IMultiplayerSettingsProvider _multiplayerSettingsProvider;
        private readonly IGameInteractionService _gameInteractionService;
        private readonly IDiceRollStorage _rollStorage;

        public const int LocalHostPlayerId = -1;

        private NetworkGameStage Status => _game?.Stage ?? NetworkGameStage.None;

        private readonly object _actionlock = new();
        private NetworkGame _game;

        public Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }
        public Action<EndPoint> OnConnected { get; set; }
        public Action<string> OnStartGame { get; set; }

        public bool IsActive => _networkServer.IsActive;

        public bool IsInLobby => IsActive && Status == NetworkGameStage.Lobby;

        public NetworkGame CurrentGame => _game;

        public MultiplayerHost(
            ILogger<MultiplayerHost> logger,
            IGameInteractionService gameInteractionService,
            IMultiplayerSettingsProvider multiplayerSettingsProvider,
            IFileSystemService fileSystemService,
            INetworkServer networkServer,
            IDiceRollStorage rollStorage)
        {
            _logger = logger;
            _networkServer = networkServer;
            _fileSystemService = fileSystemService;
            _multiplayerSettingsProvider = multiplayerSettingsProvider;
            _gameInteractionService = gameInteractionService;
            _rollStorage = rollStorage;
        }

        public void Create(string saveFilePath, List<NetworkCharacterOwnership> characters)
        {
            if (_networkServer.IsActive)
            {
                _networkServer.Dispose();
            }

            RegisterHandlers();

            _game?.Reset();

            _game = new NetworkGame(saveFilePath)
            {
                LocalPlayerId = LocalHostPlayerId
            };

            _game.Characters.AddRange(characters);

            _networkServer.Start(_multiplayerSettingsProvider.Settings.HostPortRangeStart, _multiplayerSettingsProvider.Settings.HostPortRangeEnd);

            _logger.LogInformation("Host has been created. SavePath={savePath}, Portraits={portraits}", saveFilePath, string.Join(";", _game.Characters.Select(c => c.Portrait)));
        }

        public void UpdateSaveGame(string saveFilePath, List<NetworkCharacterOwnership> characters)
        {
            _game.SaveFilePath = saveFilePath;
            _game.Characters.Clear();
            _game.Characters.AddRange(characters);
            var host = GetHost();
            foreach (var character in characters)
            {
                character.Owner = host;
            }

            _logger.LogInformation("Notifying game characters changed. Portraits={portraits}", string.Join(";", _game.Characters.Select(c => c.Portrait)));
            var message = CreateNotifyGameCharactersChanged();
            _networkServer.SendAll(message);
        }

        public void ChangeCharacterOwner(int characterIndex, int playerIndex)
        {
            lock (_actionlock)
            {
                if (_game.Players.Count < playerIndex)
                {
                    _logger.LogError("Unable to change character owner as playerIndex is out of range. PlayersCount={playersCount}, PlayerIndex={playerIndex}", _game.Players.Count, playerIndex);
                    return;
                }

                var player = _game.Players[playerIndex];

                if (_game.Characters.Count < characterIndex)
                {
                    _logger.LogError("Unable to change character owner as characterIndex is out of range. CharacterOwnersCount={characterOwnersCount}, CharacterIndex={characterIndex}", _game.Characters.Count, characterIndex);
                    return;
                }

                var character = _game.Characters[characterIndex];
                character.Owner = player;
                _logger.LogInformation("New character owner. CharacterName={characterName}, PlayerId={playerId}, PlayerName={playerName}", character.Name, player.Id, player.Name);

                var charactersOwnerChanged = CreateNotifyCharactersOwnerChanged();
                _networkServer.SendAll(charactersOwnerChanged);
            }
        }

        public void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation)
        {
            // TODO: current trigger couldn't be used in combat
            if (_game.Combat != null)
            {
                return;
            }

            _logger.LogInformation("Moving character. Name={characterName}, Destination={destination}", characterName, destination);
            var message = new NotifyCharacterMove
            {
                CharacterName = characterName,
                DestinationX = destination.X,
                DestinationY = destination.Y,
                DestinationZ = destination.Z,
                Delay = delay,
                Orientation = orientation
            };
            _networkServer.SendAll(message);
        }

        public void Dispose()
        {
            _logger.LogInformation("Dispose");

            lock (_actionlock)
            {
                _game?.Reset();
            }

            _networkServer.Dispose();
        }

        public bool CanControlCharacter(string unitId)
        {
            if (_game == null)
            {
                return false;
            }

            var realCharacterId = _gameInteractionService.GetPetOwnerId(unitId) ?? unitId;

            var character = GetCharacterOwnership(realCharacterId);

            return character == null || character.Owner != null && character.Owner.Id == _game.LocalPlayerId;
        }

        public bool ReadyChanged()
        {
            var player = _game.Players.First(p => p.Id == _game.LocalPlayerId); // host should be always present
            var readyChanged = new PlayerReadyStatusChanged { PlayerId = player.Id, IsReady = !player.IsReady };
            OnPlayerReadyStatusChanged(player.Id, readyChanged);
            return readyChanged.IsReady;
        }

        public void Start()
        {
            _logger.LogInformation("Starting game...");
            // it should be fine to block current thread
            var content = _fileSystemService.GetFile(_game.SaveFilePath);
            if (content == null)
            {
                _logger.LogError("Unable to start a game due to missing save file. Path={savePath}", _game.SaveFilePath);
                return;
            }

            _game.Stage = NetworkGameStage.Initializing;
            var gameStageChanged = new NotifyGameStageChanged { Stage = _game.Stage.ToString() };
            _networkServer.SendAll(gameStageChanged);

            lock (_actionlock)
            {
                var saveGameMessageAssigned = new NotifySaveGameAssigned { Content = content, IsForceLoad = false };
                _logger.LogInformation("Sending save game file content to all players. Size={saveFileSize}", saveGameMessageAssigned.Content.Length);
                _networkServer.SendAll(saveGameMessageAssigned);
                _game.Stage = NetworkGameStage.WaitingForPlayersInitialization;
                _logger.LogInformation("Waiting for players to confirm delivery. GameStatus={gameStatus}", _game.Stage);
                GetHost().IsSyncedToStartGame = true;
            }

            TryStartGame();
        }

        public void GameLoaded()
        {
            _logger.LogInformation("Game loaded");

            // assumption: should be done after each area load aswell
            SoftReset();

            PartyChanged();

            _gameInteractionService.Pause(true);

            var host = GetHost();
            host.IsLoading = false;

            TryUnpauseGame();
        }

        /// <summary>
        /// Reloads current party characters and tries to merge ownership
        /// </summary>
        public void PartyChanged()
        {
            _logger.LogInformation("Updating current characters & merging ownership");

            // could be synced from host, but state is the same anyway
            var partyCharacters = _gameInteractionService.GetPartyPlayers();
            if (partyCharacters.Count == 0)
            {
                return;
            }

            var oldCharacters = _game.Characters.ToList();
            _game.Characters = [.. partyCharacters];
            var defaultOwner = GetPlayer(_game.LocalPlayerId);
            foreach (var character in _game.Characters)
            {
                var existingOwnershipConfiguration = oldCharacters.FirstOrDefault(old =>
                    old.Name == character.Name || old.Name.Contains(character.Name));
                if (existingOwnershipConfiguration?.Owner != null)
                {
                    character.Owner = existingOwnershipConfiguration.Owner;
                    _logger.LogInformation("Character ownership has been preserved. CharacterId={characterId}, CharacterName={characterName}, Owner={ownerId}", character.UnitId, character.Name, character.Owner.Id);
                    continue;
                }

                character.Owner = defaultOwner;
                _logger.LogInformation("Character ownership has been assigned to default player (host). CharacterId={characterId}, CharacterName={characterName}, Owner={ownerId}", character.UnitId, character.Name, character.Owner.Id);
            }
        }

        public void Pause()
        {
            _logger.LogInformation("Sending pausing notification");
            var message = new NotifyGamePauseChanged { IsPaused = true };
            _networkServer.SendAll(message);
        }

        public void Unpause()
        {
            _logger.LogInformation("Sending unpausing notification");
            var message = new NotifyGamePauseChanged { IsPaused = false };
            _networkServer.SendAll(message);
        }

        public void LeaveArea(string areaExitId)
        {
            _logger.LogInformation("Sending NotifyPartyLeaveArea. AreaExitId={areaExitId}", areaExitId);
            var message = new NotifyPartyLeaveArea { AreaExitId = areaExitId };
            _networkServer.SendAll(message);
        }

        public void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer)
        {
            _logger.LogInformation("Showing dialog Cue. DialogName={dialogName}, CueName={cueName}, HasSystemAnswer={hasSystemAnswer}", dialogName, cueName, hasSystemAnswer);
            if (hasSystemAnswer)
            {
                _gameInteractionService.SetDialogContinueButtonState(false);
            }

            if (_game.Dialog != null && _game.Dialog.Name != dialogName)
            {
                _logger.LogWarning("Previous dialog has not been disposed correctly. PreviousDialogName={previousDialogName}, CurrentDialogName={currentDialogName}", _game.Dialog.Name, dialogName);
                _game.Dialog = null;
            }

            _game.Dialog ??= new NetworkDialog(dialogName);
            _game.Dialog.CurrentCueName = cueName;
            AddCueWitness(cueName, _game.LocalPlayerId);

            TryEnableDialogContinueButton();
        }

        public bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId)
        {
            _logger.LogInformation("Select Dialog Answer. DialogName={dialogName}, CueName={cueName} Answer={answer}, IsExitAnswer={isExitAnswer}, ManualUnitSelectionId={unitId}", dialogName, cueName, answerName, isExitAnswer, manualUnitSelectionId);

            var missingPlayers = GetPlayersWhoHaveNotSeenCueYet(cueName);
            if (missingPlayers.Count > 0)
            {
                _logger.LogWarning("Some players haven't seen the dialog yet. Players={playerNames}", string.Join(";", missingPlayers.Select(p => p.Name)));
                return false;
            }

            _game.Dialog.Answer = new NetworkDialogAnswer
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

            // resets all suggested cue answers
            _gameInteractionService.MarkSuggestedDialogAnswers([]);
            _game.Dialog.AnswerSuggestions.Clear();

            return true;
        }

        public void SendSelectedAnswer()
        {
            if (_game.Dialog == null)
            {
                _logger.LogError("Unable to send dialog answer because dialog is null");
                return;
            }

            if (_game.Dialog.Answer == null)
            {
                _logger.LogWarning("Answer is not set, most likely it's a first dialog cue or cutscene intermission. DialogName={dialogName}", _game.Dialog.Name);
                return;
            }

            _logger.LogInformation("Sending selected answer to clients. DialogName={dialogName}, CueName={cueName}, AnswerName={answerName}, ManualUnitSelectionId={unitId}", _game.Dialog.Name, _game.Dialog.Answer.CueName, _game.Dialog.Answer.AnswerName, _game.Dialog.Answer.ManualUnitSelectionId);

            var message = new NotifyDialogCueAnswerSelected
            {
                DialogName = _game.Dialog.Name,
                CueName = _game.Dialog.Answer.CueName,
                AnswerName = _game.Dialog.Answer.AnswerName,
                ManualUnitSelectionId = _game.Dialog.Answer.ManualUnitSelectionId
            };

            _networkServer.SendAll(message);
            _game.Dialog.Answer = null;
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
            _logger.LogInformation("Sending NotifyDialogStarted to all clients. DialogName={dialogName}, TargetUnitId={targetUnitId}, InitiatorUnitId={initiatorUnitId}, MapObjectId={mapObjectId}, SpeakerKey={speakerKey}",
                message.DialogName, message.TargetUnitId, message.InitiatorUnitId, message.MapObjectId, message.SpeakerKey);

            _networkServer.SendAll(message);
            return true;
        }

        public void CombatStarted()
        {
            _logger.LogInformation("Combat started");
            if (_game.Combat != null)
            {
                _logger.LogWarning("Previous combat has not been disposed correctly");
            }

            _game.Combat = new NetworkCombat();

            // it's impossible to differentiate rolls between multiple combats
            _rollStorage.Reset<InitiativeRoll>();
            _rollStorage.Reset<AttackWithWeaponRoll>();
        }

        public void CombatEnded()
        {
            _logger.LogInformation("Combat ended");
            if (_game.Combat == null)
            {
                _logger.LogWarning("Combat has not been started correctly");
            }

            _game.Combat = null;
        }

        public bool CanInitializeCombat()
        {
            // host is never blocked as combat initialization (initiative rolls) are required for a clients to proceed
            return true;
        }

        public bool CanContinueCombat()
        {
            if (_game.Combat == null)
            {
                return true;
            }

            // it's a bit random when we start blocking this continuation
            // anyway both 0 and 1 rounds are fine to start syncing as combat is already initializated at these points
            if (_game.Combat.Round <= 1 && !_game.Combat.IsInitialized)
            {
                var unitsInCombat = _gameInteractionService.GetUnitsInCombat();
                var message = new NotifyCombatStarted
                {
                    Units = [.. unitsInCombat.Select(x => new Networking.Messages.NetworkUnit
                    {
                        Id = x.Id,
                        PositionX = x.Position.X,
                        PositionY = x.Position.Y,
                        PositionZ = x.Position.Z
                    })]
                };
                _networkServer.SendAll(message);
                _game.Combat.IsInitialized = true;
                _game.Combat.PlayersCombatInitialization.TryAdd(_game.LocalPlayerId, true);
                _logger.LogInformation("Sending NotifyCombatStarted. UnitsInCombat={unitsCount}", message.Units.Count);
            }

            return _game.Combat.PlayersCombatInitialization.Count >= _game.Players.Count;
        }

        public bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound)
        {
            _logger.LogInformation("OnBeforeStartTurn. UnitId={unitId}, ActingInSurpriseRound={actingInSurpriseRound}", unitId, actingInSurpriseRound);

            if (_game.Combat.Turn != null && _game.Combat.Turn.IsInProgress)
            {
                _logger.LogInformation("Turn start is allowed.");
                return true;
            }

            _game.Combat.Turn = new NetworkCombatTurn
            {
                UnitId = unitId,
                IsInProgress = false,
                IsActingInSurpriseRound = actingInSurpriseRound,
                IsLocalPlayer = CanControlCharacter(unitId),
                IsAI = _gameInteractionService.IsUnitAI(unitId)
            };

            _logger.LogInformation("Turn start has been initialized. UnitId={unitId}, IsLocalPlayer={isLocalPlayer}, IsAI={isAI}, IsActingInSurpriseRound={isActingInSurpriseRound}",
                unitId, _game.Combat.Turn.IsLocalPlayer, _game.Combat.Turn.IsAI, _game.Combat.Turn.IsActingInSurpriseRound);

            AddCombatTurnStartInitialization(_game.LocalPlayerId, _game.Combat.Round, unitId);

            TryStartCombatTurn();

            return false;
        }

        public bool OnBeforeEndTurn(string unitId)
        {
            if (_game.Combat.Turn == null)
            {
                _logger.LogInformation("Turn end is allowed.");
                return true;
            }

            // game calls this hook constantly even if you skip original (FYI: but this is not the case for OnBeforeStartTurn)
            // but we need to setup everything only once
            if (!_game.Combat.Turn.IsInProgress)
            {
                return false;
            }

            _logger.LogInformation("OnBeforeEndTurn. UnitId={unitId}", unitId);

            AddCombatTurnEndInitialization(_game.LocalPlayerId, _game.Combat.Round, unitId);
            TryEndCombatTurn();

            return false;
        }

        public void CombatRoundStarted(int round)
        {
            _logger.LogInformation("Combat round started. Round={round}", round);
            if (_game.Combat == null)
            {
                _logger.LogWarning("Combat has not started yet");
                return;
            }

            _game.Combat.Round = round;
        }

        public int GetCombatRound()
        {
            return _game.Combat?.Round ?? 0;
        }

        public void ForceLoadGame(string savePath)
        {
            _logger.LogInformation("Notifying clients to force load save game. Path={savePath}", savePath);

            var message = new NotifySaveGameAssigned
            {
                Content = _fileSystemService.GetFile(savePath),
                IsForceLoad = true
            };

            foreach (var player in _game.Players)
            {
                player.IsLoading = true;
            }

            _networkServer.SendAll(message);
        }

        public bool ShouldStoreRoll()
        {
            return _game.Combat == null // everything happens on host outside of combat
                || !_game.Combat.IsInitialized // combat initialization phase (initiative rolls)
                || _game.Combat.Turn == null // could happen when some new NPC joins midfight in midturns, e.g. Anevia in prologue
                || _game.Combat.Turn.IsAI // AI turn happens on host side first, so clients are getting their AI rolls from host
                || _game.Combat.Turn.IsLocalPlayer; // other MP players are getting rolls from turn owner
        }

        public NetworkDiceRoll RetrieveRoll(int networkDiceRollId, string unitId)
        {
            _logger.LogInformation("Retrieving roll from other player. RollId={rollId}, UnitId={unitId}", networkDiceRollId, unitId);

            var realCharacterId = _gameInteractionService.GetPetOwnerId(unitId) ?? unitId;
            var character = GetCharacterOwnership(realCharacterId);
            if (character == null)
            {
                _logger.LogError("Unable to find character. UnitId={unitId}", realCharacterId);
                return null;
            }

            var message = new RollRequest { RollId = networkDiceRollId };
            var playerId = character.Owner.Id;
            var response = _networkServer.SendAndWaitFor<RollResponse>(playerId, message);
            if (response == null)
            {
                _logger.LogError("Unable to retrieve roll from player. PlayerId={playerId}, RollId={rollId}", playerId, networkDiceRollId);
                return null;
            }

            if (response.Roll == null)
            {
                _logger.LogError("Player returned null roll. PlayerId={playerId}, RollId={rollId}", playerId, networkDiceRollId);
                return null;
            }

            return new NetworkDiceRoll
            {
                Result = response.Roll.Result,
                RollHistory = [.. response.Roll.RollHistory]
            };
        }

        private void TryStartCombatTurn()
        {
            if (_game.Combat.Turn == null)
            {
                // could only happen when client starts turn before the host
                _logger.LogWarning("Trying to start turn, but it has not been initialized yet. Round={round}", _game.Combat.Round);
                return;
            }

            if (_game.Combat.Turn.IsInProgress)
            {
                _logger.LogWarning("Turn is already in progress. Round={round}, UnitId={unitId}", _game.Combat.Round, _game.Combat.Turn.UnitId);
                return;
            }

            var canStart = false;

            lock (_actionlock)
            {
                var key = GetTurnInitializationKey(_game.Combat.Round, _game.Combat.Turn.UnitId);
                canStart = _game.Combat.PlayersTurnStartInitialization.TryGetValue(key, out var players) && players.Count >= _game.Players.Count;
            }

            if (canStart)
            {
                var message = new NotifyCombatTurnStarted { Round = _game.Combat.Round, UnitId = _game.Combat.Turn.UnitId };
                _networkServer.SendAll(message);

                _game.Combat.Turn.IsInProgress = true;
                _gameInteractionService.StartTurnBasedCombatTurn(_game.Combat.Turn.IsActingInSurpriseRound);
            }
        }

        private void AddCombatTurnStartInitialization(long playerId, int round, string unitId)
        {
            lock (_actionlock)
            {
                var key = GetTurnInitializationKey(round, unitId);
                _game.Combat.PlayersTurnStartInitialization.AddOrUpdate(key,
                    key => new HashSet<long>([playerId]),
                    (key, existing) =>
                    {
                        existing.Add(playerId);
                        return existing;
                    });
            }
        }

        private void AddCombatTurnEndInitialization(long playerId, int round, string unitId)
        {
            lock (_actionlock)
            {
                var key = GetTurnInitializationKey(round, unitId);
                _game.Combat.PlayersTurnEndInitialization.AddOrUpdate(key,
                    key => new HashSet<long>([playerId]),
                    (key, existing) =>
                    {
                        existing.Add(playerId);
                        return existing;
                    });
            }
        }

        private string GetTurnInitializationKey(int round, string unitId)
        {
            return $"{round}|{unitId}";
        }

        private void TryEndCombatTurn()
        {
            if (_game.Combat.Turn == null)
            {
                // could only happen when client starts turn before the host
                _logger.LogWarning("Trying to end already ended turn. Round={round}", _game.Combat.Round);
                return;
            }

            var canEnd = false;

            lock (_actionlock)
            {
                var key = GetTurnInitializationKey(_game.Combat.Round, _game.Combat.Turn.UnitId);
                canEnd = _game.Combat.PlayersTurnEndInitialization.TryGetValue(key, out var players) && players.Count >= _game.Players.Count;
            }

            if (canEnd)
            {
                var message = new NotifyCombatTurnEnded { Round = _game.Combat.Round, UnitId = _game.Combat.Turn.UnitId };
                _networkServer.SendAll(message);

                _game.Combat.Turn = null;
                _gameInteractionService.EndTurnBasedCombatTurn();
                return;
            }

            _game.Combat.Turn.IsInProgress = false;
        }

        private NetworkCharacterOwnership GetCharacterOwnership(string unitId)
        {
            return _game.Characters.FirstOrDefault(c => string.Equals(c.UnitId, unitId, StringComparison.OrdinalIgnoreCase));
        }

        private void SoftReset()
        {
            _logger.LogInformation("Doing soft reset");
            _game.Dialog = null;
            _game.SaveFilePath = null;
            _game.Combat = null;
            _rollStorage.Reset();
        }

        private void AddCueWitness(string cueName, long playerId)
        {
            if (_game.Dialog == null)
            {
                _logger.LogError("Trying to add witness to null dialog. CueName={cueName}, PlayerId={playerId}", cueName, playerId);
                return;
            }

            _game.Dialog.CueViews.AddOrUpdate(cueName, (key) => new HashSet<long>([playerId]), (key, existing) =>
            {
                existing.Add(playerId);
                return existing;
            });

            _logger.LogInformation("Cue witness has been added. CueName={cueName}, PlayerId={playerId}", cueName, playerId);
        }

        private List<NetworkPlayer> GetPlayersWhoHaveNotSeenCueYet(string cueName)
        {
            if (_game.Dialog == null)
            {
                _logger.LogWarning("Trying to get cue players, but dialog is null. CueName={cueName}", cueName);
                return [];
            }

            if (!_game.Dialog.CueViews.TryGetValue(cueName, out var cueViews))
            {
                _logger.LogWarning("Specified cue doesn't exist in the views history. CueName={cueName}", cueName);
                return [];
            }

            var players = _game.Players.Where(p => !cueViews.Contains(p.Id)).ToList();
            return players;
        }

        private void TryEnableDialogContinueButton()
        {
            if (_game.Dialog == null)
            {
                _logger.LogError("Unable to enable continue button because current dialog is null");
                return;
            }

            var currentCue = _game.Dialog.CurrentCueName;
            if (string.IsNullOrEmpty(currentCue))
            {
                _logger.LogError("Current CueName is not set for the dialog");
                return;
            }

            var missingPlayers = GetPlayersWhoHaveNotSeenCueYet(currentCue);
            if (missingPlayers.Count > 0)
            {
                _logger.LogInformation("Cannot proceed with dialog yet. CurrentCue={currentCue}, MissingPlayers={missingPlayers}", currentCue, string.Join(";", missingPlayers.Select(x => x.Name)));
                return;
            }

            _logger.LogInformation("All players have witnessed current cue. CueName={cueName}", currentCue);
            _gameInteractionService.SetDialogContinueButtonState(true);
        }

        private void TryUnpauseGame()
        {
            var canUnpause = false;

            lock (_actionlock)
            {
                canUnpause = _game.Players.All(p => !p.IsLoading);
            }

            if (canUnpause)
            {
                _logger.LogInformation("All players have finished loading. Game will be unpaused");
                _game.Stage = NetworkGameStage.Playing;
                _gameInteractionService.Pause(false);
            }
        }

        private void OnGameLoaded(long playerId, GameLoaded loaded)
        {
            _logger.LogInformation("OnGameLoaded. PlayerId={playerId}", playerId);
            lock (_actionlock)
            {
                var player = GetPlayer(playerId);
                if (player == null)
                {
                    _logger.LogError("Can't set loading status for missing player. PlayerId={playerId}", playerId);
                    return;
                }

                player.IsLoading = false;
            }

            TryUnpauseGame();
        }

        private void TryStartGame()
        {
            var canStart = false;

            lock (_actionlock)
            {
                canStart = _game.Players.All(p => p.IsSyncedToStartGame);
            }

            if (canStart)
            {
                _logger.LogInformation("Starting game");
                foreach (var player in _game.Players)
                {
                    player.IsLoading = true;
                }

                _networkServer.SendAll(new NotifyGameStarted());
                OnStartGame?.Invoke(_game.SaveFilePath);
            }
        }

        private NotifyCharactersOwnerChanged CreateNotifyCharactersOwnerChanged()
        {
            _game.Characters.Select((character, index) => new Networking.Messages.NetworkCharacterOwner { CharacterIndex = index, PlayerId = character.Owner.Id });
            var charactersOwnerChanged = new NotifyCharactersOwnerChanged
            {
                Owners = [.. _game.Characters.Select((character, index) => new Networking.Messages.NetworkCharacterOwner { CharacterIndex = index, PlayerId = character.Owner.Id })]
            };

            return charactersOwnerChanged;
        }

        private NetworkPlayer GetHost()
        {
            return _game.Players.First(f => f.Id == _game.LocalPlayerId);
        }

        private void RegisterHandlers()
        {
            _networkServer.OnClientConnected = OnPlayerConnected;
            _networkServer.OnClientDisconnected = OnPlayerDisconnected;
            _networkServer.OnServerStarted = OnServerStarted;

            _networkServer
                // this is special case when client sends notify as usually all notifies are sent by host only
                // we need to load game ASAP on both host/clients
                .Register<NotifySaveGameAssigned>(OnNotifySaveGameAssigned)

                .Register<PlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
                .Register<PlayerNameResponse>(OnPlayerNameResponse)
                .Register<PlayerSaveGameSyncChanged>(OnPlayerSaveGameSyncChanged)
                .Register<CharacterMove>(OnCharacterMove)
                .Register<GameLoaded>(OnGameLoaded)
                .Register<GamePauseChanged>(OnGamePauseChanged)
                .Register<RollRequest>(OnRollRequest)
                .Register<CueWitnessed>(OnCueWitnessed)
                .Register<DialogCueAnswerSuggested>(OnDialogCueAnswerSuggested)
                .Register<StartDialogRequested>(OnStartDialogRequested)
                .Register<ClientCombatInitialized>(OnClientCombatInitialized)
                .Register<ClientCombatTurnStarted>(OnClientCombatTurnStarted)
                .Register<ClientCombatTurnEnded>(OnClientCombatTurnEnded)
                ;
        }

        private void OnClientCombatTurnEnded(long playerId, ClientCombatTurnEnded ended)
        {
            _logger.LogInformation($"Received {nameof(ClientCombatTurnEnded)}. PlayerId={{playerId}}, Round={{round}}, UnitId={{unitId}}", playerId, ended.Round, ended.UnitId);
            AddCombatTurnEndInitialization(playerId, ended.Round, ended.UnitId);
            TryEndCombatTurn();
        }

        private void OnClientCombatTurnStarted(long playerId, ClientCombatTurnStarted started)
        {
            _logger.LogInformation($"Received {nameof(ClientCombatTurnStarted)}. PlayerId={{playerId}}, Round={{round}}, UnitId={{unitId}}", playerId, started.Round, started.UnitId);
            AddCombatTurnStartInitialization(playerId, started.Round, started.UnitId);
            TryStartCombatTurn();
        }

        private void OnNotifySaveGameAssigned(long playerId, NotifySaveGameAssigned assigned)
        {
            _logger.LogInformation($"Received {nameof(NotifySaveGameAssigned)}. PlayerId={{playerId}}, IsForceLoad={{isForceLoad}}, SaveGameSize={{saveGameSize}}", playerId, assigned.IsForceLoad, assigned.Content.Length);

            _networkServer.SendAllExcept(playerId, assigned);
        }

        private void OnClientCombatInitialized(long playerId, ClientCombatInitialized initialized)
        {
            _logger.LogInformation("Received OnClientCombatInitialized. PlayerId={playerId}", playerId);
            if (_game.Combat == null)
            {
                _logger.LogWarning("Received client initialization, but combat is null. PlayerId={playerId}", playerId);
                return;
            }

            if (!_game.Combat.PlayersCombatInitialization.TryAdd(playerId, true))
            {
                _logger.LogWarning("Received duplicate client initialization. PlayerId={playerId}", playerId);
            }
        }

        private async void OnStartDialogRequested(long playerId, StartDialogRequested requested)
        {
            _logger.LogInformation("Received StartDialogRequested. PlayerId={playerId}, DialogName={dialogName}, TargetUnitId={targetUnitId}, InitiatorUnitId={initiatorUnitId}, MapObjectId={mapObjectId}, SpeakerKey={speakerKey}",
                playerId, requested.DialogName, requested.TargetUnitId, requested.InitiatorUnitId, requested.MapObjectId, requested.SpeakerKey);

            var hasStartedDialog = await _gameInteractionService.StartDialogAsync(requested.DialogName, requested.TargetUnitId, requested.InitiatorUnitId, requested.MapObjectId, requested.SpeakerKey);
            if (!hasStartedDialog)
            {
                _logger.LogInformation("Host dialog is already in progress. Sending dialog confirmation");
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
        }

        private void OnDialogCueAnswerSuggested(long playerId, DialogCueAnswerSuggested suggested)
        {
            _logger.LogInformation("Received DialogCueAnswerSuggested. PlayerId={playerId}, DialogName={dialogName}, CueName={cueName}, AnswerName={answerName}", playerId, suggested.DialogName, suggested.CueName, suggested.AnswerName);

            if (_game.Dialog == null)
            {
                _logger.LogError("Received dialog answer suggestion, but there is no active dialog right now. SuggestedDialogName={suggestedDialogName}, SuggestedCueName={suggestedCueName}, SuggestedAnswer={suggestedAnswerName}", suggested.DialogName, suggested.CueName, suggested.AnswerName);
                return;
            }

            if (!string.Equals(_game.Dialog.Name, suggested.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Dialog suggestion has mismatched dialog name. SuggestedDialogName={suggestedDialogName}, CurrentDialogName={currentCueName}", suggested.DialogName, _game.Dialog.Name);
                return;
            }

            if (!string.Equals(_game.Dialog.CurrentCueName, suggested.CueName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Dialog suggestion has mismatched cue name. SuggestedCueName={suggestedCueName}, CurrentCueName={currentCueName}", suggested.CueName, _game.Dialog.CurrentCueName);
                return;
            }

            _game.Dialog.AnswerSuggestions.AddOrUpdate(playerId, suggested.AnswerName, (key, existing) =>
            {
                return suggested.AnswerName;
            });

            List<NetworkDialogAnswerSuggestion> suggestions = [.. _game.Dialog.AnswerSuggestions.GroupBy(x => x.Value, x => x.Key).Select(x => new NetworkDialogAnswerSuggestion { AnswerName = x.Key, Players = [.. x] })];
            _gameInteractionService.MarkSuggestedDialogAnswers(suggestions);

            var notifyMessage = new NotifyDialogCueAnswerSuggested
            {
                DialogName = suggested.DialogName,
                CueName = suggested.CueName,
                Suggestions = [.. suggestions.Select(x => new Networking.Messages.NetworkDialogAnswerSuggestion { AnswerName = x.AnswerName, Players = [.. x.Players] })],
            };
            _networkServer.SendAll(notifyMessage);
        }

        private void OnCueWitnessed(long playerId, CueWitnessed witnessed)
        {
            _logger.LogInformation("Received CueWitnessed. PlayerId={playerId}, DialogName={dialogName}, CueName={cueName}", playerId, witnessed.DialogName, witnessed.CueName);
            if (_game.Dialog == null)
            {
                _logger.LogError("Received cue witness, but there is no active dialog right now. WitnessedDialogName={witnessedDialogName}, WitnessedCueName={witnessedCueName}", witnessed.DialogName, witnessed.CueName);
                return;
            }

            if (!string.Equals(_game.Dialog.Name, witnessed.DialogName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Cue witness has mismatched dialog. WitnessedDialogName={witnessedDialogName}, CurrentDialogName={currentCueName}", witnessed.DialogName, _game.Dialog.Name);
                return;
            }

            AddCueWitness(witnessed.CueName, playerId);
            TryEnableDialogContinueButton();
        }

        private void OnRollRequest(long playerId, RollRequest request)
        {
            _logger.LogInformation("Received RollRequest. PlayerId={playerId}, RollId={rollId}", playerId, request.RollId);
            var roll = _rollStorage.Get(request.RollId, playerId);

            var response = new RollResponse
            {
                Roll = roll == null ? null : new Networking.Messages.NetworkDiceRoll { Result = roll.Result, RollHistory = [.. roll.RollHistory] },
            };

            _logger.LogInformation("Sending RollResponse. RollResult={rollResult}", roll?.Result ?? 0);
            _networkServer.Send(playerId, response);
        }

        private void OnGamePauseChanged(long playerId, GamePauseChanged pauseChanged)
        {
            _logger.LogInformation("Received GamePauseChanged. PlayerId={playerId}, IsPaused={isPaused}", playerId, pauseChanged.IsPaused);
            var message = new NotifyGamePauseChanged { IsPaused = pauseChanged.IsPaused };
            _networkServer.SendAllExcept(playerId, message);
            _gameInteractionService.Pause(pauseChanged.IsPaused);
        }

        private void OnCharacterMove(long playerId, CharacterMove move)
        {
            _logger.LogInformation("Received CharacterMove. PlayerId={playerId}, CharacterName={characterName}, DestinationX={x}, DestinationY={y}, DestinationZ={z}", playerId, move.CharacterName, move.DestinationX, move.DestinationY, move.DestinationZ);

            var destination = new Vector3(move.DestinationX, move.DestinationY, move.DestinationZ);
            _gameInteractionService.MoveCharacter(move.CharacterName, destination, move.Delay, move.Orientation);

            var notifyMove = new NotifyCharacterMove
            {
                CharacterName = move.CharacterName,
                DestinationX = move.DestinationX,
                DestinationY = move.DestinationY,
                DestinationZ = move.DestinationZ,
                Delay = move.Delay,
                Orientation = move.Orientation
            };
            _networkServer.SendAllExcept(playerId, notifyMove);
        }

        private void OnPlayerSaveGameSyncChanged(long playerId, PlayerSaveGameSyncChanged changed)
        {
            _logger.LogInformation("Received PlayerSaveGameSyncChanged. PlayerId={playerId}, SyncStatus={syncStatus}", playerId, changed.IsSynced);
            lock (_actionlock)
            {
                var player = GetPlayer(playerId);
                if (player == null)
                {
                    _logger.LogError("Player is missing. Game won't start. Player Id={playerId}", playerId);
                    return;
                }

                player.IsSyncedToStartGame = changed.IsSynced;
            }

            TryStartGame();
        }

        private void OnPlayerReadyStatusChanged(long playerId, PlayerReadyStatusChanged readyStatusChanged)
        {
            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(playerId);
                if (existingPlayer == null)
                {
                    _logger.LogWarning("Can't find existing player. PlayerId={playerId}", playerId);
                    return;
                }

                existingPlayer.IsReady = readyStatusChanged.IsReady;

                OnPlayersChanged?.Invoke(_game.Players);
                _logger.LogInformation("Sending ready status changed. PlayerId={playerId}, IsReady={isReady}", playerId, existingPlayer.IsReady);
                _networkServer.SendAll(readyStatusChanged);
            }
        }

        private void OnPlayerNameResponse(long playerId, PlayerNameResponse response)
        {
            try
            {
                _logger.LogInformation("Player name received. PlayerId={playerId}, Name={name}", playerId, response?.Name);
                lock (_actionlock)
                {
                    var existingPlayer = GetPlayer(playerId);
                    if (existingPlayer == null)
                    {
                        _logger.LogWarning("Can't process player name update because player doesn't exist. PlayerId={playerId}, Name={name}", playerId, response?.Name);
                        return;
                    }

                    if (string.IsNullOrEmpty(response.Name))
                    {
                        _logger.LogWarning("Can't process player name update because player name is missing. PlayerId={playerId}, Name={name}", playerId, response?.Name);
                        return;
                    }

                    existingPlayer.Name = response.Name;

                    OnPlayersChanged?.Invoke(_game.Players);

                    var players = _game.Players.Select(x => new Networking.Messages.NetworkPlayer { Id = x.Id, Name = x.Name, IsReady = x.IsReady }).ToList();
                    var playersChanged = new NotifyPlayersChanged { Players = players };
                    _logger.LogInformation("Sending players changed to ALL players");
                    _networkServer.SendAll(playersChanged);

                    var notifyGameCharactersChanged = CreateNotifyGameCharactersChanged();
                    _logger.LogInformation("Sending GameCharactersChanged to new player. PlayerId={playerId}", playerId);
                    _networkServer.Send(playerId, notifyGameCharactersChanged);

                    _logger.LogInformation("Sending CharactersOwnerChanged to new player. PlayerId={playerId}", playerId);
                    var charactersOwnerChanged = CreateNotifyCharactersOwnerChanged();
                    _networkServer.Send(playerId, charactersOwnerChanged);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to handle player name response");
                throw;
            }
        }

        private void OnPlayerConnected(long playerId)
        {
            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(playerId);
                if (existingPlayer != null)
                {
                    _logger.LogWarning("Player already exists. PlayerId={playerId}", playerId);
                    return;
                }

                var player = new NetworkPlayer(playerId);
                _game.Players.Add(player);
                _logger.LogInformation("Sending player name request. PlayerId={playerId}", playerId);
                _networkServer.Send(playerId, new PlayerNameRequest { ClientPlayerId = playerId });
            }
        }

        private void OnPlayerDisconnected(long playerId)
        {
            lock (_actionlock)
            {
                var existingPlayer = GetPlayer(playerId);
                if (existingPlayer == null)
                {
                    _logger.LogWarning("Nothing to cleanup since player doesn't exist. PlayerId={playerId}", playerId);
                    return;
                }

                _game.Players.Remove(existingPlayer);
                if (!string.IsNullOrEmpty(existingPlayer.Name))
                {
                    OnPlayersChanged?.Invoke(_game.Players);
                }

                // TODO: send updates to other clients
                _logger.LogError("Player disconnection is not synced with other players");

                if (_game.Stage == NetworkGameStage.Playing)
                {
                    _gameInteractionService.ShowModalMessage($"Player {existingPlayer.Name} has left the game");
                }
            }
        }

        private void OnServerStarted(EndPoint endpoint)
        {
            var hostPlayer = new NetworkPlayer(LocalHostPlayerId)
            {
                Name = _multiplayerSettingsProvider.Settings.PlayerName
            };

            _game.Players.Add(hostPlayer);
            _game.Endpoint = endpoint;

            foreach (var character in _game.Characters)
            {
                character.Owner = hostPlayer;
            }

            OnConnected?.Invoke(endpoint);
            OnPlayersChanged?.Invoke(_game.Players);
        }

        private NetworkPlayer GetPlayer(long playerId)
        {
            return _game.Players.FirstOrDefault(p => p.Id == playerId);
        }

        private NotifyGameCharactersChanged CreateNotifyGameCharactersChanged()
        {
            var message = new NotifyGameCharactersChanged
            {
                Characters = [.. _game.Characters.Select(c => new Networking.Messages.NetworkCharacterOwnership { Name = c.Name, Portrait = c.Portrait })]
            };
            return message;
        }
    }
}
