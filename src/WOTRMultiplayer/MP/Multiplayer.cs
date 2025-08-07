using System;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.GameInteraction.Contexts;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.UI.Menu;

namespace WOTRMultiplayer.MP
{
    public class Multiplayer : IMultiplayer
    {
        private IMultiplayerWindow _multiplayerWindow;
        private ILobbyWindow _lobbyWindow;

        private readonly ILobbyWindowController _lobbyWindowController;
        private readonly IGameInteractionService _gameInteractionService;
        private readonly ILogger _logger;
        private readonly IMultiplayerActorAccessor _multiplayerActorAccessor;

        public IUIFactory Factory { get; private set; }

        public IUniqueIdGenerator IdGenerator { get; private set; }

        public bool IsActive => _multiplayerActorAccessor.Current != null && _multiplayerActorAccessor.Current.IsActive;

        public bool IsInCombat => _multiplayerActorAccessor.Current != null && _multiplayerActorAccessor.Current.IsInCombat;

        public NetworkExecutionContext ExecutionContext => _gameInteractionService.ExecutionContext;

        public Multiplayer(
            ILogger<Multiplayer> logger,
            IUIFactory uiFactory,
            ILobbyWindowController lobbyWindowController,
            IMultiplayerActorAccessor multiplayerActorAccessor,
            IGameInteractionService gameInteractionService,
            IUniqueIdGenerator uniqueIdGenerator)
        {
            _logger = logger;
            Factory = uiFactory;
            _multiplayerActorAccessor = multiplayerActorAccessor;
            _lobbyWindowController = lobbyWindowController;
            _gameInteractionService = gameInteractionService;
            IdGenerator = uniqueIdGenerator;
        }

        public bool InitializeMultiplayer(InitializeMultiplayerContext context)
        {
            if (_multiplayerActorAccessor.Host.IsActive)
            {
                _logger.LogWarning("Multiplayer host has not been properly disposed. Verify exit game/main menu handles");
                _multiplayerActorAccessor.Host.Dispose();
            }

            if (_multiplayerActorAccessor.Client.IsActive)
            {
                _logger.LogWarning("Multiplayer client has not been properly disposed. Verify exit game/main menu handlers");
                _multiplayerActorAccessor.Client.Dispose();
            }

            _multiplayerWindow = Factory.InitializeMultiplayerWindow(context, ShowMultiplayerWindow);

            return true;
        }

        public void TerminateMultiplayer()
        {
            _logger.LogInformation("Disposing both multiplayer host/client");
            _multiplayerActorAccessor.Host.Dispose();
            _multiplayerActorAccessor.Client.Dispose();
            _lobbyWindowController.ResetOwnerContent(LobbyWindowOwner.EscMenu);
            _lobbyWindowController.OnCharacterOwnerChanged = null;
            _logger.LogInformation("Disposing Esc menu window game objects");
            Factory.DestroyLobbyWindow(_lobbyWindow);
            _logger.LogInformation("Disposing stored rolls");
        }

        public void InitializeEscMenuLobbyWindow(InitializeEscMenuLobbyWindowContext context)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _logger.LogInformation("Creating Esc menu multiplayer lobby window");
            _lobbyWindow = Factory.InitializeEscMenuLobbyWindow(context, _multiplayerActorAccessor.Host.IsActive, ShowEscMenuMultiplayerLobby);

            _lobbyWindow.GetGameConnectivity = _multiplayerActorAccessor.Current.GetGameConnectivity;
            _lobbyWindow.GetPlayers = _multiplayerActorAccessor.Current.GetPlayers;
            _lobbyWindow.GetCharacters = _multiplayerActorAccessor.Current.GetCharacters;

            _lobbyWindow.AssignLobbyController(_lobbyWindowController);

            _lobbyWindowController.OnCharacterOwnerChanged = OnLobbyCharacterOwnerChanged;
        }

        public void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.MoveNonCombatCharacter(unitId, destination, delay, orientation);
        }


        public string GetMultiplayerOwnerName(string unitId)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return null;
            }

            return _multiplayerActorAccessor.Current.GetMultiplayerOwnerName(unitId);

        }
        public bool IsControlledByLocalPlayer(string unitId)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return false;
            }

            return _multiplayerActorAccessor.Current.IsControlledByLocalPlayer(unitId);
        }

        public bool StartGameMode(GameModeType type)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return true;
            }

            var allowedToRun = type != GameModeType.EscMode && type != GameModeType.FullScreenUi;
            _logger.LogInformation("Trying to start GameModeType. Mode={mode}, AllowedToRun={allowedToRun}", type.Name, allowedToRun);

            if (type == GameModeType.Pause)
            {
                _multiplayerActorAccessor.Current.Pause();
            }

            return allowedToRun;
        }

        public bool StopGameMode(GameModeType type)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return true;
            }

            _logger.LogInformation("Trying to stop GameModeType. Mode={mode}", type.Name);

            if (type == GameModeType.Pause)
            {
                _multiplayerActorAccessor.Current.Unpause();
            }

            return true;
        }

        public bool CanLeaveArea()
        {
            return !_multiplayerActorAccessor.Client.IsActive;
        }

        public void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnAfterCueShow(dialogName, cueName, hasSystemAnswer);
        }

        public bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return true;
            }

            var shouldContinueExecution = _multiplayerActorAccessor.Current.OnBeforeSelectDialogAnswer(dialogName, cueName, answerName, isExitAnswer, manualUnitSelectionId);
            return shouldContinueExecution;
        }

        public void OnAfterPlayDialogCue()
        {
            if (_multiplayerActorAccessor.Client.IsActive)
            {
                return;
            }

            _multiplayerActorAccessor.Host.SendSelectedAnswer();
        }

        public bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return true;
            }

            _logger.LogInformation("Start dialog. DialogueName={dialogName},  TargetUnitId={targetUnitId}, InitiatorUnitId={initiatorUnitId}, MapObjectId={mapObjectId}, SpeakerKey={speakerKey}",
                dialogName, targetUnitId, initiatorUnitId, mapObjectId, speakerKey);

            return _multiplayerActorAccessor.Current.StartDialog(dialogName, targetUnitId, initiatorUnitId, mapObjectId, speakerKey);
        }

        public bool CanTickUnitCombatPrepareController()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                return _multiplayerActorAccessor.Current.CanInitializeCombat();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiatize combat");
                throw;
            }
        }

        public bool CanTickCombatController()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                return _multiplayerActorAccessor.Current.CanContinueCombat();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to continue combat");
                throw;
            }
        }

        public bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return true;
            }

            return _multiplayerActorAccessor.Current.OnBeforeStartTurn(unitId, actingInSurpriseRound);
        }

        public bool OnBeforeEndTurn(string unitId)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return true;
            }

            return _multiplayerActorAccessor.Current.OnBeforeEndTurn(unitId);
        }

        public void ForceLoadGame(SaveInfo saveInfo)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            // extra validation is not required since everything is already validated by the game
            var savePath = saveInfo.FolderName;
            _logger.LogInformation("Force load game. Save={saveLocation}, GameId={gameId}", savePath, saveInfo.GameId);
            _multiplayerActorAccessor.Current.ForceLoadGame(savePath, saveInfo.GameId);
        }

        public bool IsControlledByPlayers(string unitId)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return true;
            }

            var result = _multiplayerActorAccessor.Current.IsControlledByPlayers(unitId);
            return result;
        }

        public void OnClickUnit(NetworkClick click)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnClickUnit(click);
        }

        public void OnClickGround(NetworkClick click)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnClickGround(click);
        }

        public void OnClickMapObject(NetworkClick click)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnClickMapObject(click);
        }

        public void OnAbilityUse(NetworkAbility ability)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnAbilityUse(ability);
        }

        public void OnToggleActivatableAbility(NetworkActivatableAbility activatableAbilityUse)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnToggleActivatableAbility(activatableAbilityUse);
        }

        public NetworkActionsState GetActionsState()
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return null;
            }

            var actionsState = _gameInteractionService.GetActionsState();
            return actionsState;
        }

        public bool CanLootUnit(string initiatorUnitId)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return true;
            }

            var isControlledByLocalPlayer = _multiplayerActorAccessor.Current.IsControlledByLocalPlayer(initiatorUnitId);
            return isControlledByLocalPlayer;
        }

        public void OnLootContainer(NetworkLootContainer container)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLootContainer(container);
        }

        public void OnDropItem(NetworkDropItem dropItem)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnDropItem(dropItem);
        }

        public void OnChangeActiveHandEquipmentSet(NetworkActiveHandEquipmentSet set)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnChangeActiveHandEquipmentSet(set);
        }

        public void OnInteractWithMapObjectOvertip(NetworkOvertip networkOvertip)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnInteractWithMapObjectOvertip(networkOvertip);
        }

        public bool CanUnitJoinCombat(string unitId)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return true;
            }

            return _multiplayerActorAccessor.Current.CanUnitJoinCombat(unitId);
        }

        public void OnPerceptionCheck(NetworkPerceptionCheck check)
        {
            if (!_multiplayerActorAccessor.Host.IsActive)
            {
                return;
            }

            _multiplayerActorAccessor.Host.OnPerceptionCheck(check);
        }

        public bool CanMakePerceptionCheck(string unitId, string mapObjectId)
        {
            if (_multiplayerActorAccessor.Client.IsActive)
            {
                var perceptionCheck = _gameInteractionService.ExecutionContext?.PerceptionCheck;
                if (perceptionCheck == null)
                {
                    return false;
                }

                return string.Equals(unitId, perceptionCheck.UnitId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(mapObjectId, perceptionCheck.MapObjectId, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private void ShowEscMenuMultiplayerLobby()
        {
            _logger.LogInformation("Show lobby window");
            _lobbyWindow.Show(true);
        }

        private void ShowMultiplayerWindow()
        {
            _logger.LogInformation("Show Multiplayer window");
            _multiplayerWindow.Show(true);
        }

        private void OnLobbyCharacterOwnerChanged(int characterIndex, int playerIndex)
        {
            _logger.LogInformation("OnLobbyCharacterOwnerChanged. CharacterIndex={charIndex}, PlayerIndex={playerIndex}", characterIndex, playerIndex);
            _multiplayerActorAccessor.Host.ChangeCharacterOwner(characterIndex, playerIndex);
        }
    }
}
