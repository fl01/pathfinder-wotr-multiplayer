using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        private readonly IDiceRollStorage _diceRollStorage;

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
            IDiceRollStorage diceRollStorage)
        {
            _logger = logger;
            _networkServer = networkServer;
            _fileSystemService = fileSystemService;
            _multiplayerSettingsProvider = multiplayerSettingsProvider;
            _gameInteractionService = gameInteractionService;
            _diceRollStorage = diceRollStorage;
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

        public void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation)
        {
            // TODO: current trigger couldn't be used in combat
            if (_game.Combat != null)
            {
                return;
            }

            _logger.LogInformation("Sending NotifyCharacterMove. UnitId={unitId}, Destination={destination}, Delay={delay}, Orientation={orientation}", unitId, destination, delay, orientation);
            var message = new NotifyCharacterMove
            {
                UnitId = unitId,
                Destination = new Networking.Messages.NetworkVector3(destination.X, destination.Y, destination.Z),
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
            //_logger.LogInformation("Sending pausing notification");
            //var message = new NotifyGamePauseChanged { IsPaused = true };
            //_networkServer.SendAll(message);
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
            _logger.LogInformation("Sending dialog started to all clients. DialogName={dialogName}, TargetUnitId={targetUnitId}, InitiatorUnitId={initiatorUnitId}, MapObjectId={mapObjectId}, SpeakerKey={speakerKey}",
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
            _diceRollStorage.Reset<InitiativeRoll>();
            _diceRollStorage.Reset<AttackWithWeaponRoll>();
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

            if (_game.Combat.Round == 1 && !_game.Combat.IsInitialized)
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
                _logger.LogInformation($"Sending {nameof(NotifyCombatStarted)}. UnitsInCombat={{unitsCount}}", message.Units.Count);
            }

            return _game.Combat.PlayersCombatInitialization.Count >= _game.Players.Count;
        }

        public bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound)
        {
            try
            {
                if (_game.Combat.Turn != null && _game.Combat.Turn.IsInProgress)
                {
                    _logger.LogInformation("Turn start is allowed. UnitId={unitId}", unitId);
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

                _logger.LogInformation("OnBeforeStartTurn. UnitId={unitId}, IsLocalPlayer={isLocalPlayer}, IsAI={isAI}, IsActingInSurpriseRound={isActingInSurpriseRound}",
                    unitId, _game.Combat.Turn.IsLocalPlayer, _game.Combat.Turn.IsAI, _game.Combat.Turn.IsActingInSurpriseRound);

                AddCombatTurnStartInitialization(_game.LocalPlayerId, _game.Combat.Round, unitId);

                TryStartCombatTurn();

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to process {nameof(OnBeforeStartTurn)}. UnitId={{unitId}}, ActingInSurpriseRound={{actingInSurpriseRound}}", unitId, actingInSurpriseRound);
                throw;
            }
        }

        public bool OnBeforeEndTurn(string unitId)
        {
            try
            {
                if (_game.Combat.Turn == null)
                {
                    _logger.LogInformation("Turn end is allowed. UnitId={unitId}", unitId);
                    return true;
                }

                // game calls this hook constantly even if you skip original (FYI: but this is not the case for OnBeforeStartTurn)
                // but we need to setup everything only once
                if (!_game.Combat.Turn.IsInProgress)
                {
                    return false;
                }

                _logger.LogInformation("OnBeforeEndTurn. UnitId={unitId}, IsLocalPlayer={isLocalPlayer}, IsAI={isAI}, IsActingInSurpriseRound={isActingInSurpriseRound}, IsInProgress={isInProgress}",
                             unitId, _game.Combat.Turn.IsLocalPlayer, _game.Combat.Turn.IsAI, _game.Combat.Turn.IsActingInSurpriseRound, _game.Combat.Turn.IsInProgress);

                AddCombatTurnEndInitialization(_game.LocalPlayerId, _game.Combat.Round, unitId);
                _game.Combat.Turn.IsInProgress = false;

                if (!_game.Combat.Turn.IsAI && _game.Combat.Turn.IsLocalPlayer)
                {
                    _logger.LogInformation("Sending turn ended to other clients. UnitId={unitId}", unitId);
                    var message = new CombatTurnEnded { Round = _game.Combat.Round, UnitId = unitId };
                    _networkServer.SendAll(message);
                }

                TryEndCombatTurn();

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to process {nameof(OnBeforeEndTurn)}. UnitId={{unitId}},", unitId);
                throw;
            }
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

            var waitForRollTimeout = TimeSpan.FromSeconds(10);
            var message = new RollRequest { RollId = networkDiceRollId, Timeout = waitForRollTimeout };
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

        public void OnClickUnit(NetworkClick click)
        {
            if (!(_game.Combat?.Turn?.IsLocalPlayer ?? false) || _gameInteractionService.CombatTurnHasBeenFinished())
            {
                return;
            }

            _logger.LogInformation("Sending unit click. TargetUnitId={targetUnitId}, VectorPathCount={pathCount}", click.TargetUnitId, click.VectorPath.Count);

            var message = new NotifyUnitClicked
            {
                Click = new Networking.Messages.NetworkClick
                {
                    Button = click.Button,
                    MuteEvents = click.MuteEvents,
                    SelectedUnits = click.SelectedUnits,
                    TargetUnitId = click.TargetUnitId,
                    WorldPosition = new Networking.Messages.NetworkVector3(click.WorldPosition.X, click.WorldPosition.Y, click.WorldPosition.Z),
                    VectorPath = [.. click.VectorPath.Select(x => new Networking.Messages.NetworkVector3(x.X, x.Y, x.Z))]
                }
            };

            _networkServer.SendAll(message);
        }

        public void OnClickGround(NetworkClick click)
        {
            if (!(_game.Combat?.Turn?.IsLocalPlayer ?? false) || _gameInteractionService.CombatTurnHasBeenFinished())
            {
                return;
            }

            _logger.LogInformation("Sending ground click. WorldPosition={worldPosition}, VectorPathCount={pathCount}, SelectedUnits={selectedUnits}", click.WorldPosition, click.VectorPath.Count, string.Join(";", click.SelectedUnits));
            var message = new NotifyGroundClicked
            {
                Click = new Networking.Messages.NetworkClick
                {
                    Button = click.Button,
                    MuteEvents = click.MuteEvents,
                    SelectedUnits = click.SelectedUnits,
                    TargetUnitId = click.TargetUnitId,
                    WorldPosition = new Networking.Messages.NetworkVector3(click.WorldPosition.X, click.WorldPosition.Y, click.WorldPosition.Z),
                    VectorPath = [.. click.VectorPath.Select(x => new Networking.Messages.NetworkVector3(x.X, x.Y, x.Z))]
                }
            };

            _networkServer.SendAll(message);
        }

        public void OnClickWithSelectedAbility(NetworkClick click)
        {
            if (!(_game.Combat?.Turn?.IsLocalPlayer ?? false) || _gameInteractionService.CombatTurnHasBeenFinished())
            {
                return;
            }

            _logger.LogInformation("Sending ability click. TargetUnitId={targetUnitId}, AbilityId={abilityId}, WorldPosition={worldPosition}, VectorPathCount={pathCount}",
                click.TargetUnitId, click.Ability.Id, click.WorldPosition, click.VectorPath.Count);

            var message = new NotifyAbilityClicked
            {
                Click = new Networking.Messages.NetworkClick
                {
                    Button = click.Button,
                    MuteEvents = click.MuteEvents,
                    SelectedUnits = click.SelectedUnits,
                    TargetUnitId = click.TargetUnitId,
                    Ability = new Networking.Messages.NetworkAbility
                    {
                        Id = click.Ability.Id,
                        SpellbookId = click.Ability.SpellbookId
                    },
                    WorldPosition = new Networking.Messages.NetworkVector3(click.WorldPosition.X, click.WorldPosition.Y, click.WorldPosition.Z),
                    VectorPath = [.. click.VectorPath.Select(x => new Networking.Messages.NetworkVector3(x.X, x.Y, x.Z))]
                }
            };

            _networkServer.SendAll(message);
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

            List<NetworkPlayer> notReadyPlayers = [.. _game.Players];
            lock (_actionlock)
            {
                var key = GetTurnInitializationKey(_game.Combat.Round, _game.Combat.Turn.UnitId);
                if (_game.Combat.PlayersTurnStartInitialization.TryGetValue(key, out var readyToStartPlayers))
                {
                    notReadyPlayers.RemoveAll(p => readyToStartPlayers.Contains(p.Id));
                }

                if (notReadyPlayers.Count == 0)
                {
                    var message = new NotifyCombatTurnStarted { Round = _game.Combat.Round, UnitId = _game.Combat.Turn.UnitId };
                    _networkServer.SendAll(message);

                    _game.Combat.Turn.IsInProgress = true;
                    _gameInteractionService.StartTurnBasedCombatTurn(_game.Combat.Turn.IsActingInSurpriseRound);
                    return;
                }
            }

            _logger.LogInformation("Turn can't be started yet. Round={round}, UnitId={unitId}, NotReadyPlayers={notReadyPlayers}", _game.Combat.Round, _game.Combat.Turn.UnitId, string.Join(";", notReadyPlayers.Select(p => p.Name)));
        }

        private void AddCombatTurnStartInitialization(long playerId, int round, string unitId)
        {
            try
            {
                lock (_actionlock)
                {
                    var key = GetTurnInitializationKey(round, unitId);
                    _game.Combat.PlayersTurnStartInitialization.AddOrUpdate(key,
                        key => new HashSet<long>(collection: [playerId]),
                        (key, existing) =>
                        {
                            existing.Add(playerId);
                            return existing;
                        });

                    _logger.LogInformation("TurnStart initialization has been added. Key={key}, PlayersCount={playersCount}, KeysCount={keysCount}", key, _game.Combat.PlayersTurnStartInitialization[key].Count, _game.Combat.PlayersTurnStartInitialization.Keys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to add TurnStart initialization. PlayerId={playerId}, Round={round}, UnitId={unitId}", playerId, round, unitId);
                throw;
            }
        }

        private void AddCombatTurnEndInitialization(long playerId, int round, string unitId)
        {
            try
            {
                lock (_actionlock)
                {
                    var key = GetTurnInitializationKey(round, unitId);
                    _game.Combat.PlayersTurnEndInitialization.AddOrUpdate(key,
                        key => new HashSet<long>(collection: [playerId]),
                        (key, existing) =>
                        {
                            existing.Add(playerId);
                            return existing;
                        });
                    _logger.LogInformation("TurnEnd initialization has been added. Key={key}, PlayersCount={playersCount}, KeysCount={keysCount}", key, _game.Combat.PlayersTurnEndInitialization[key].Count, _game.Combat.PlayersTurnStartInitialization.Keys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to add TurnEnd initialization. PlayerId={playerId}, Round={round}, UnitId={unitId}", playerId, round, unitId);
                throw;
            }
        }

        private string GetTurnInitializationKey(int round, string unitId)
        {
            return $"{round}-{unitId}";
        }

        private void TryEndCombatTurn()
        {
            if (_game.Combat.Turn == null)
            {
                // could only happen when client starts turn before the host
                _logger.LogWarning("Trying to end already ended turn. Round={round}", _game.Combat.Round);
                return;
            }

            List<NetworkPlayer> notReadyPlayers = [.. _game.Players];
            lock (_actionlock)
            {
                var key = GetTurnInitializationKey(_game.Combat.Round, _game.Combat.Turn.UnitId);
                if (_game.Combat.PlayersTurnEndInitialization.TryGetValue(key, out var readyToEndPlayers))
                {
                    notReadyPlayers.RemoveAll(p => readyToEndPlayers.Contains(p.Id));
                }

                if (notReadyPlayers.Count == 0)
                {
                    var message = new NotifyCombatTurnEnded { Round = _game.Combat.Round, UnitId = _game.Combat.Turn.UnitId };
                    _networkServer.SendAll(message);

                    if (_game.Combat.Turn != null)
                    {
                        _game.Combat.Turn = null;
                        _gameInteractionService.EndTurnBasedCombatTurn();
                    }
                    return;
                }
            }

            _logger.LogInformation("Turn can't be ended yet. NotReadyPlayers={notReadyPlayers}", string.Join(";", notReadyPlayers.Select(p => p.Name)));
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
            _diceRollStorage.Reset();
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

        private void OnClientGameLoaded(long playerId, ClientGameLoaded loaded)
        {
            _logger.LogInformation($"Received {nameof(ClientGameLoaded)}. PlayerId={{playerId}}", playerId);
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
                // we need to load game ASAP on both host/remaining clients
                .Register<NotifySaveGameAssigned>(OnNotifySaveGameAssigned)
                .Register<NotifyUnitClicked>(OnNotifyUnitClicked)
                .Register<NotifyGroundClicked>(OnNotifyGroundClicked)
                .Register<NotifyAbilityClicked>(OnNotifyAbilityClicked)

                // this is kinda special as well as the client is blocking the game loop thread until `RollResponse` is received
                .Register<RollRequest>(OnRollRequest)

                .Register<PlayerReadyStatusChanged>(OnPlayerReadyStatusChanged)
                .Register<PlayerNameResponse>(OnPlayerNameResponse)
                .Register<PlayerSaveGameSyncChanged>(OnPlayerSaveGameSyncChanged)
                .Register<CharacterMove>(OnCharacterMove)
                .Register<ClientGameLoaded>(OnClientGameLoaded)
                .Register<GamePauseChanged>(OnGamePauseChanged)
                .Register<CueWitnessed>(OnCueWitnessed)
                .Register<DialogCueAnswerSuggested>(OnDialogCueAnswerSuggested)
                .Register<StartDialogRequested>(OnStartDialogRequested)
                .Register<ClientCombatInitialized>(OnClientCombatInitialized)
                .Register<ClientCombatTurnStarted>(OnClientCombatTurnStarted)
                .Register<CombatTurnEnded>(OnCombatTurnEnded)
                ;
        }

        private void OnNotifyAbilityClicked(long playerId, NotifyAbilityClicked clicked)
        {
            _logger.LogInformation($"Received {nameof(NotifyAbilityClicked)}. AbilityId={{abilityId}}, TargetUnitId={{targetUnitId}}, SelectedUnitId={{selectedUnits}}, WorldPosition={{worldPosition}}", clicked.Click.Ability.Id, clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count, clicked.Click.WorldPosition);
            if (_game.Combat == null)
            {
                _logger.LogWarning($"{nameof(NotifyAbilityClicked)} is ignored out of combat");
                return;
            }

            var click = new NetworkClick
            {
                Button = clicked.Click.Button,
                MuteEvents = clicked.Click.MuteEvents,
                SelectedUnits = clicked.Click.SelectedUnits,
                Ability = new NetworkAbility
                {
                    Id = clicked.Click.Ability.Id,
                    SpellbookId = clicked.Click.Ability.SpellbookId
                },
                TargetUnitId = clicked.Click.TargetUnitId,
                WorldPosition = new NetworkVector3(clicked.Click.WorldPosition.X, clicked.Click.WorldPosition.Y, clicked.Click.WorldPosition.Z),
                VectorPath = [.. clicked.Click.VectorPath.Select(v => new NetworkVector3(v.X, v.Y, v.Z))]
            };

            _gameInteractionService.ClickAbilityInCombat(click);

            _logger.LogInformation($"Resending {nameof(NotifyAbilityClicked)} to other players");
            _networkServer.SendAllExcept(playerId, clicked);
        }

        private void OnNotifyGroundClicked(long playerId, NotifyGroundClicked clicked)
        {
            _logger.LogInformation($"Received {nameof(NotifyGroundClicked)}. PlayerId={{playerId}}, SelectedUnitId={{selectedUnits}}, WorldPosition={{worldPosition}}", playerId, clicked.Click.SelectedUnits.Count, clicked.Click.WorldPosition);
            if (_game.Combat == null)
            {
                _logger.LogWarning($"{nameof(NotifyGroundClicked)} is ignored out of combat");
                return;
            }

            var click = new NetworkClick
            {
                Button = clicked.Click.Button,
                MuteEvents = clicked.Click.MuteEvents,
                SelectedUnits = clicked.Click.SelectedUnits,
                WorldPosition = new NetworkVector3(clicked.Click.WorldPosition.X, clicked.Click.WorldPosition.Y, clicked.Click.WorldPosition.Z),
                VectorPath = [.. clicked.Click.VectorPath.Select(v => new NetworkVector3(v.X, v.Y, v.Z))]
            };

            _gameInteractionService.ClickGroundInCombat(click);

            _logger.LogInformation($"Resending {nameof(NotifyGroundClicked)} to other players");
            _networkServer.SendAllExcept(playerId, click);
        }

        private void OnNotifyUnitClicked(long playerId, NotifyUnitClicked clicked)
        {
            _logger.LogInformation($"Received {nameof(NotifyUnitClicked)}. PlayerId={{playerId}}, TargetUnitId={{targetUnitId}}, SelectedUnits={{selectedUnits}}", playerId, clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);
            if (_game.Combat == null)
            {
                _logger.LogWarning($"{nameof(NotifyUnitClicked)} is ignored out of combat");
                return;
            }

            var click = new NetworkClick
            {
                Button = clicked.Click.Button,
                MuteEvents = clicked.Click.MuteEvents,
                SelectedUnits = clicked.Click.SelectedUnits,
                TargetUnitId = clicked.Click.TargetUnitId,
                WorldPosition = new NetworkVector3(clicked.Click.WorldPosition.X, clicked.Click.WorldPosition.Y, clicked.Click.WorldPosition.Z),
                VectorPath = [.. clicked.Click.VectorPath.Select(v => new NetworkVector3(v.X, v.Y, v.Z))]
            };

            _gameInteractionService.ClickUnitInCombat(click);

            _logger.LogInformation($"Resending {nameof(NotifyUnitClicked)} to other players");
            _networkServer.SendAllExcept(playerId, click);
        }

        private void OnCombatTurnEnded(long playerId, CombatTurnEnded ended)
        {
            _logger.LogInformation($"Received {nameof(CombatTurnEnded)}. PlayerId={{playerId}}, Round={{round}}, UnitId={{unitId}}", playerId, ended.Round, ended.UnitId);

            AddCombatTurnEndInitialization(playerId, ended.Round, ended.UnitId);

            if (!_game.Combat.Turn.IsAI && !_game.Combat.Turn.IsLocalPlayer)
            {
                _logger.LogInformation("Current turn is owned by another player. Ending it locally.  PlayerId={playerId}, Round={round}, UnitId={unitId}", playerId, ended.Round, ended.UnitId);
                OnBeforeEndTurn(ended.UnitId);
                _networkServer.SendAllExcept(playerId, ended);
                return;
            }

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
            _logger.LogInformation($"Received {nameof(ClientCombatInitialized)}. PlayerId={{playerId}}", playerId);
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
            _logger.LogInformation($"Received {nameof(StartDialogRequested)}. PlayerId={{playerId}}, DialogName={{dialogName}}, TargetUnitId={{targetUnitId}}, InitiatorUnitId={{initiatorUnitId}}, MapObjectId={{mapObjectId}}, SpeakerKey={{speakerKey}}",
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
            _logger.LogInformation($"Received {nameof(DialogCueAnswerSuggested)}. PlayerId={{playerId}}, DialogName={{dialogName}}, CueName={{cueName}}, AnswerName={{answerName}}", playerId, suggested.DialogName, suggested.CueName, suggested.AnswerName);

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
            _logger.LogInformation($"Received {nameof(CueWitnessed)}. PlayerId={{playerId}}, DialogName={{dialogName}}, CueName={{cueName}}", playerId, witnessed.DialogName, witnessed.CueName);
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

        private async void OnRollRequest(long playerId, RollRequest request)
        {
            _logger.LogInformation($"Received {nameof(RollRequest)}. PlayerId={{playerId}}, RollId={{rollId}}", playerId, request.RollId);
            // some events would occur at around the same time on client/host, but client MUST receive this dice roll from the host
            var roll = await _diceRollStorage.GetAsync(request.RollId, playerId, request.Timeout);

            var response = new RollResponse
            {
                Roll = roll == null ? null : new Networking.Messages.NetworkDiceRoll { Result = roll.Result, RollHistory = [.. roll.RollHistory] },
            };

            _logger.LogInformation("Sending roll response. RollResult={rollResult}", roll?.Result ?? 0);
            _networkServer.Send(playerId, response);
        }

        private void OnGamePauseChanged(long playerId, GamePauseChanged pauseChanged)
        {
            _logger.LogInformation($"Received {nameof(GamePauseChanged)}. PlayerId={{playerId}}, IsPaused={{isPaused}}", playerId, pauseChanged.IsPaused);
            var message = new NotifyGamePauseChanged { IsPaused = pauseChanged.IsPaused };
            _networkServer.SendAllExcept(playerId, message);
            _gameInteractionService.Pause(pauseChanged.IsPaused);
        }

        private void OnCharacterMove(long playerId, CharacterMove move)
        {
            _logger.LogInformation($"Received {nameof(CharacterMove)}. PlayerId={{playerId}}, UnitId={{unitId}}, Destination={{destination}}", playerId, move.UnitId, move.Destination);

            var destination = new NetworkVector3(move.Destination.X, move.Destination.Y, move.Destination.Z);
            _gameInteractionService.MoveNonCombatCharacter(move.UnitId, destination, move.Delay, move.Orientation);

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
            _logger.LogInformation($"Received {nameof(PlayerSaveGameSyncChanged)}. PlayerId={{playerId}}, SyncStatus={{syncStatus}}", playerId, changed.IsSynced);
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
                _logger.LogInformation($"Received {nameof(PlayerNameResponse)}. PlayerId={{playerId}}, Name={{name}}", playerId, response?.Name);
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
