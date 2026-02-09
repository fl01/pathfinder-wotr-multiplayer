using System;
using System.Collections.Generic;
using System.Reflection;
using Kingmaker.GameModes;
using Kingmaker.Items.Slots;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Hotkeys;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.ActionBar;
using WOTRMultiplayer.Entities.Area;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Entities.Movement;
using WOTRMultiplayer.Entities.NewGame;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Entities.Vendor;
using WOTRMultiplayer.Services.GameInteraction.Contexts;
using WOTRMultiplayer.UI.Windows;

namespace WOTRMultiplayer.Services
{
    public class Multiplayer : IMultiplayer
    {
        private IMultiplayerWindow _multiplayerWindow;
        private ILobbyWindow _lobbyWindow;

        private readonly ILobbyWindowController _lobbyWindowController;
        private readonly IGameInteractionService _gameInteractionService;
        private readonly ICombatInteractionService _combatInteractionService;
        private readonly ILogger _logger;
        private readonly IMultiplayerActorAccessor _multiplayerActorAccessor;
        private readonly IHotkeysService _hotkeysService;

        public IUIFactory Factory { get; private set; }

        public IValueGenerator ValueGenerator { get; private set; }

        public bool IsActive => _multiplayerActorAccessor.Current != null && _multiplayerActorAccessor.Current.IsActive;

        public bool IsInCombat => _multiplayerActorAccessor.Current != null && _multiplayerActorAccessor.Current.IsInCombat;

        public RemoteExecutionContext RemoteContext => _gameInteractionService.RemoteContext;

        public Multiplayer(
            ILogger<Multiplayer> logger,
            IUIFactory uiFactory,
            ILobbyWindowController lobbyWindowController,
            IMultiplayerActorAccessor multiplayerActorAccessor,
            IGameInteractionService gameInteractionService,
            ICombatInteractionService combatInteractionService,
            IHotkeysService hotkeysService,
            IValueGenerator valueGenerator)
        {
            _logger = logger;
            Factory = uiFactory;
            _multiplayerActorAccessor = multiplayerActorAccessor;
            _lobbyWindowController = lobbyWindowController;
            _gameInteractionService = gameInteractionService;
            _combatInteractionService = combatInteractionService;
            _hotkeysService = hotkeysService;
            ValueGenerator = valueGenerator;
        }

        public bool InitializeMultiplayer(InitializeMultiplayerContext context)
        {
            if (_multiplayerActorAccessor.Host.IsActive)
            {
                _logger.LogWarning("Multiplayer host has not been properly disposed. Verify exit game/main menu handles");
                _multiplayerActorAccessor.Host.Reset();
            }

            if (_multiplayerActorAccessor.Client.IsActive)
            {
                _logger.LogWarning("Multiplayer client has not been properly disposed. Verify exit game/main menu handlers");
                _multiplayerActorAccessor.Client.Reset();
            }

            _multiplayerWindow = Factory.InitializeMultiplayerWindow(context, ShowMultiplayerWindow);
            _hotkeysService.Initialize();

            return true;
        }

        public void TerminateMultiplayer()
        {
            _logger.LogInformation("Disposing both multiplayer host/client");
            _multiplayerActorAccessor.Host.Reset();
            _multiplayerActorAccessor.Client.Reset();
            _multiplayerActorAccessor.Client.OnCharacterOwnerChanged = null;
            _lobbyWindowController.ResetOwnerContent(LobbyWindowOwner.EscMenu);
            _lobbyWindowController.OnCharacterOwnerChanged = null;
            _logger.LogInformation("Disposing Esc menu window game objects");
            Factory.DestroyLobbyWindow(_lobbyWindow);
        }

        public void InitializeEscMenuLobbyWindow()
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _logger.LogInformation("Creating Esc menu multiplayer lobby window");
            _lobbyWindow = Factory.InitializeEscMenuLobbyWindow(_lobbyWindowController, ShowMultiplayerLobbyWindow);

            _lobbyWindow.GetGameConnectivity = _multiplayerActorAccessor.Current.GetGameConnectivity;
            _lobbyWindow.GetPlayers = _multiplayerActorAccessor.Current.GetPlayers;
            _lobbyWindow.GetCharacters = _multiplayerActorAccessor.Current.GetCharacters;
            _lobbyWindow.GetIsHost = () => _multiplayerActorAccessor.Host.IsActive;

            _lobbyWindow.WithController(_lobbyWindowController);

            if (_multiplayerActorAccessor.Host.IsActive)
            {
                _lobbyWindowController.OnCharacterOwnerChanged = OnMultiplayerHostLobbyCharacterOwnerChanged;
            }

            if (_multiplayerActorAccessor.Client.IsActive)
            {
                _multiplayerActorAccessor.Client.OnCharacterOwnerChanged = OnMultiplayerClientCharacterOwnerChanged;
            }
        }

        public void MoveNonCombatCharacter(NetworkCharacterMove networkCharacterMove)
        {
            try
            {

                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.MoveNonCombatCharacter(networkCharacterMove);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while moving character outside of combat. UnitId={UnitId}", networkCharacterMove.UnitId);
                throw;
            }
        }

        public string GetCharacterOwnerName(string unitId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return null;
                }

                return _multiplayerActorAccessor.Current.GetCharacterOwnerName(unitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting character owner name. UnitId={UnitId}", unitId);
                throw;
            }
        }

        public bool IsControlledByLocalPlayer(string unitId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return false;
                }

                return _multiplayerActorAccessor.Current.IsControlledByLocalPlayer(unitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking if controlled by local player. UnitId={UnitId}", unitId);
                throw;
            }
        }

        public void OnStartGameMode(GameModeType type)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnStartGameMode(type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting game mode. GameMode={GameMode}", type);
                throw;
            }
        }

        public void OnStopGameMode(GameModeType type)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnStopGameMode(type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping game mode. GameMode={GameMode}", type);
                throw;
            }
        }

        public bool CanInitiateAreaTransitions()
        {
            return _multiplayerActorAccessor.Current != null && _multiplayerActorAccessor.Host.IsActive;
        }

        public void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnAfterCueShow(dialogName, cueName, hasSystemAnswer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error after cue show. DialogName={DialogName}, CueName={CueName}, HasSystemAnswer={HasSystemAnswer}", dialogName, cueName, hasSystemAnswer);
                throw;
            }
        }

        public bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var shouldContinueExecution = _multiplayerActorAccessor.Current.OnBeforeSelectDialogAnswer(dialogName, cueName, answerName, isExitAnswer, manualUnitSelectionId);
                return shouldContinueExecution;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error before selecting dialog answer. DialogName={DialogName}, CueName={CueName}", dialogName, cueName);
                throw;
            }
        }

        public void OnAfterPlayDialogCue()
        {
            try
            {
                if (_multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.SendSelectedAnswer();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error after playing dialog cue");
                throw;
            }
        }

        public bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                return _multiplayerActorAccessor.Current.StartDialog(dialogName, targetUnitId, initiatorUnitId, mapObjectId, speakerKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting dialog. DialogName={DialogName}", dialogName);
                throw;
            }
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
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                return _multiplayerActorAccessor.Current.OnBeforeStartTurn(unitId, actingInSurpriseRound);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting turn");
                throw;
            }
        }

        public bool OnBeforeEndTurn(string unitId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var canEnd = _multiplayerActorAccessor.Current.OnBeforeEndTurn(unitId);
                return canEnd;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while ending turn");
                throw;
            }
        }

        public void ForceLoadGame(string gameId, string savePath)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.ForceLoadGame(gameId, savePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while force loading the game");
                throw;
            }
        }

        public bool IsControlledByPlayers(string unitId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var result = _multiplayerActorAccessor.Current.IsControlledByPlayers(unitId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking if unit is controlled by players");
                throw;
            }
        }

        public void OnClickUnit(NetworkClick click)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnClickUnit(click);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while clicking on unit. TargetUnitId={TargetUnitId}", click?.TargetUnitId);
                throw;
            }
        }

        public void OnClickGround(NetworkClick networkClick)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnClickGround(networkClick);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while clicking on ground. WorldPosition={WorldPosition}", networkClick?.WorldPosition);
                throw;
            }
        }

        public void OnClickMapObject(NetworkClick networkClick)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnClickMapObject(networkClick);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while clicking map object. MapObjectId={MapObjectId}", networkClick?.MapObjectId);
                throw;
            }
        }

        public void OnAbilityUse(NetworkAbilityUse networkAbilityUse)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnAbilityUse(networkAbilityUse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while using ability. AbilityName={AbilityName}", networkAbilityUse.Ability?.Name);
                throw;
            }
        }

        public void OnToggleActivatableAbility(NetworkActivatableAbility networkActivatableAbility)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnToggleActivatableAbility(networkActivatableAbility);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while toggling activatable ability. AbilityId={AbilityId}", networkActivatableAbility?.Id);
                throw;
            }
        }

        public void OnTransferInventoryItems(NetworkItemsTransfer networkItemsTransfer)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnTransferInventoryItem(networkItemsTransfer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while transfering inventory item. Source={Source}, Destination={Destination}", networkItemsTransfer.Source?.Id, networkItemsTransfer.Destination?.Id);
                throw;
            }
        }

        public void OnSkinLootContainer(NetworkLootableEntity networkLootableEntity)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnSkinLootContainer(networkLootableEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while skinning lootable entity. ContainerId={ContainerId}", networkLootableEntity.Id);
                throw;
            }
        }

        public void OnDropItem(NetworkDropItem networkDropItem)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                var context = _gameInteractionService.RemoteContext?.DropItem;
                if (context != null && string.Equals(context.UnitId, networkDropItem.OwnerEntityId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.ItemId, networkDropItem.Item.UniqueId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnDropItem(networkDropItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while dropping item. ItemId={ItemId}", networkDropItem?.Item?.UniqueId);
                throw;
            }
        }

        public void OnUseInventoryItem(NetworkUseInventoryItem useInventoryItem)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                var context = _gameInteractionService.RemoteContext?.UseInventoryItem;
                if (context != null
                    && string.Equals(context.ItemId, useInventoryItem.Item.UniqueId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.UserUnitId, useInventoryItem.UserUnitId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnUseInventoryItem(useInventoryItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while using inventory item. ItemId={ItemId}", useInventoryItem.Item?.UniqueId);
                throw;
            }
        }

        public void OnInteractWithMapObjectOvertip(NetworkOvertip networkOvertip)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                var context = _gameInteractionService.RemoteContext?.Overtip;
                if (context != null && string.Equals(context.MapObjectId, networkOvertip.MapObject.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnInteractWithMapObjectOvertip(networkOvertip);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while interacting with map object overtip. MapObjectId={MapObjectId}", networkOvertip?.MapObject?.Id);
                throw;
            }
        }

        public bool CanUnitJoinCombat(string unitId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                return _multiplayerActorAccessor.Current.CanUnitJoinCombat(unitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking if unit can join combat. UnitId={UnitId}", unitId);
                throw;
            }
        }

        public bool CanMakePerceptionCheck(string unitId, string mapObjectId)
        {
            return _multiplayerActorAccessor.Current != null && _multiplayerActorAccessor.Host.IsActive;
        }

        public bool CanMakeStealthPerceptionCheck()
        {
            return _multiplayerActorAccessor.Current != null && _multiplayerActorAccessor.Host.IsActive;
        }

        public void OnPerceptionCheck(NetworkPerceptionCheck networkPerceptionCheck)
        {
            try
            {
                if (!_multiplayerActorAccessor.Host.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnPerceptionCheck(networkPerceptionCheck);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing perception check. UnitId={UnitId}", networkPerceptionCheck.UnitId);
                throw;
            }
        }

        public void OnStealthPerceptionCheckRolled(NetworkStealthPerceptionCheck networkStealthPerceptionCheck)
        {
            try
            {
                if (!_multiplayerActorAccessor.Host.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnStealthPerceptionCheckRolled(networkStealthPerceptionCheck);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing stealth perception check. UnitId={UnitId}, Roll={Roll}", networkStealthPerceptionCheck.InitiatorId, networkStealthPerceptionCheck.Roll);
                throw;
            }
        }

        public bool CanMakeInspectionKnowledgeCheck()
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return true;
            }

            var canContinue = !_multiplayerActorAccessor.Client.IsActive;
            return canContinue;
        }

        public void OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck networkInspectionKnowledgeCheck)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnInspectionKnowledgeCheck(networkInspectionKnowledgeCheck);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing inspection knowledge check. InitiatorUnitId={InitiatorUnitId}", networkInspectionKnowledgeCheck?.InitiatorUnitId);
                throw;
            }
        }

        public bool CanMakeInspectionBuffCheck()
        {
            if (!_multiplayerActorAccessor.Client.IsActive)
            {
                return true;
            }

            return true;
        }

        public bool OnSpawnCampPlace(NetworkVector3 position)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var canContinue = _multiplayerActorAccessor.Current.OnSpawnCampPlace(position);
                return canContinue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while spawning camp place. Position={Position}", position);
                throw;
            }
        }


        public bool OnCampingUseHealingSpellsChanged(bool isActive)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                if (_multiplayerActorAccessor.Client.IsActive)
                {
                    return false;
                }

                _multiplayerActorAccessor.Host.OnCampingUseHealingSpellsChanged(isActive);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing healing spell usage change. IsActive={IsActive}", isActive);
                throw;
            }
        }

        public void OnCampingUnitsRoleChanged(List<NetworkCampingRole> networkCampingRoles)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                if (_multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnCampingUnitsRoleChanged(networkCampingRoles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing camping units role change");
                throw;
            }
        }

        public void OnCapitalModeRest()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnCapitalModeRest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting capital mode rest");
                throw;
            }
        }

        public void OnStartRest()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnStartRest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting camp rest");
                throw;
            }
        }

        public void OnStartRestSleepPhase()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnStartRestSleepPhase();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting rest sleep phase");
                throw;
            }
        }

        public bool CanUseRestUI()
        {
            return _multiplayerActorAccessor.Current != null && _multiplayerActorAccessor.Host.IsActive;
        }

        public void OnShowGroupChangerUI()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnShowGroupChangerUI();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing group changing ui");
                throw;
            }
        }

        public bool OnClickGroupChangerUnit(string unitId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var canUse = _multiplayerActorAccessor.Current.OnClickGroupChangerUnit(unitId);
                return canUse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking group manager ui permissions");
                throw;
            }
        }

        public void OnCloseGroupChangerUI()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnCloseGroupChangerPartyUI();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing group changing ui");
                throw;
            }
        }

        public void OnAcceptGroupChangerParty()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnAcceptGroupChangerParty();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while accepting group changer ui");
                throw;
            }
        }

        public void OnBeforeTryRollRestRandomEncounter()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                if (_multiplayerActorAccessor.Host.IsActive)
                {
                    _gameInteractionService.SetRandomEncounterContext(new NetworkRandomEncounterContext() { Recording = new NetworkRandomEncounter() });
                    return;
                }

                _multiplayerActorAccessor.Client.OnBeforeTryRollRestRandomEncounter();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error before rolling random encounter");
                throw;
            }
        }

        public void OnAfterTryRollRestRandomEncounter()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                if (_multiplayerActorAccessor.Host.IsActive)
                {
                    var context = _gameInteractionService.RemoteContext?.RandomEncounter;
                    _multiplayerActorAccessor.Host.OnAfterTryRollRestRandomEncounter(context);
                    return;
                }

                if (_gameInteractionService.RemoteContext?.RandomEncounter != null)
                {
                    _gameInteractionService.RemoteContext.RandomEncounter = null;
                    _combatInteractionService.UpdateIsInCombatStatus();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error after rolling random encounter", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public int GetCombatSeed()
        {
            var seed = _multiplayerActorAccessor.Current?.CombatSeed ?? 0;
            return seed;
        }

        public int GetCombatTurnSeed()
        {
            var seed = _multiplayerActorAccessor.Current?.CombatTurnSeed ?? 0;
            return seed;
        }

        public int GetCrusadeArmyCombatSeed()
        {
            return _multiplayerActorAccessor.Current?.CrusadeArmyCombatSeed ?? 0;
        }

        public int GetSessionSeed()
        {
            var seed = _multiplayerActorAccessor.Current?.SessionSeed ?? 0;
            return seed;
        }

        public int GetLoadedSaveSeed()
        {
            var seed = _multiplayerActorAccessor.Current?.LoadedSaveSeed ?? 0;
            return seed;
        }

        public int? GetCrusadeArmyCombatAreaSeed()
        {
            try
            {
                if (_multiplayerActorAccessor == null || _multiplayerActorAccessor.Host.IsActive)
                {
                    return null;
                }

                var seed = _multiplayerActorAccessor.Current.CrusadeArmyCombatAreaSeed;
                return seed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting crusade army combat seed");
                throw;
            }
        }

        public void OnInterrupRestBanterBark(NetworkRestBanter networkRestBanter)
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnInterrupRestBanterBark(networkRestBanter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while interrupting rest banter bark. BanterKey={BanterKey}", networkRestBanter.Key);
                throw;
            }
        }

        public NetworkAIAction OnAfterAISelectedAction(NetworkAIAction networkAIAction)
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return null;
                }

                var possibleOverride = _multiplayerActorAccessor.Current.OnAfterAISelectedAction(networkAIAction);
                return possibleOverride;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error after AI selected action. AIUnitId={AIUnitId}", networkAIAction?.UnitId);
                throw;
            }
        }

        public void OnTransferVendorItem(NetworkVendorItemTransfer networkVendorItemTransfer)
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return;
                }

                var vendorItemTransfer = _gameInteractionService.RemoteContext?.VendorItemTransfer;
                if (vendorItemTransfer != null && string.Equals(vendorItemTransfer.ItemId, networkVendorItemTransfer.Item.UniqueId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnTransferVendorItem(networkVendorItemTransfer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while transferring vendor item. ItemId={ItemId}", networkVendorItemTransfer?.Item?.UniqueId);
                throw;
            }
        }

        public void OnMakeVendorDeal()
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return;
                }

                if (_multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnMakeVendorDeal();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while making vendor deal");
                throw;
            }
        }

        public void OnCloseVendorWindow()
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return;
                }

                if (_multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnCloseVendorWindow();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing vendor window");
                throw;
            }
        }

        public bool CanFullyControlVendorUI()
        {
            return _multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Host.IsActive;
        }

        public void OnMemorizeSpell(string unitId, NetworkSpellSlot networkSpellSlot, NetworkAbility networkAbility)
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnMemorizeSpell(unitId, networkSpellSlot, networkAbility);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while memorizing spell. UnitId={UnitId}, SpellName={SpellName}", unitId, networkAbility.Name);
                throw;
            }
        }

        public void OnForgetSpell(string unitId, NetworkSpellSlot networkSpellSlot, NetworkAbility networkAbility)
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnForgetSpell(unitId, networkSpellSlot, networkAbility);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while forgetting spell. UnitId={UnitId}, SpellName={SpellName}", unitId, networkAbility.Name);
                throw;
            }
        }

        public void OnLevelingClassArchetypeSelected(NetworkLevelingArchetype archetype)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingClassArchetypeSelected(archetype);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing leveling class archetype selection. ArchetypeId={ArchetypeId}", archetype?.Id);
                throw;
            }
        }

        public void OnLevelingClassSelected(NetworkLevelingClass levelingClass)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingClassSelected(levelingClass);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing leveling class selection. ClassId={ClassId}", levelingClass.Id);
                throw;
            }
        }

        public void OnLevelingMythicClassSelected(string mythicClassId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingMythicClassSelected(mythicClassId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing leveling mythic class selection. MythicClassId={MythicClassId}", mythicClassId);
                throw;
            }
        }

        public void OnLevelingBodyTypeAppearanceChanged(int index)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingBodyTypeAppearanceChanged(index);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling body type appearance. Index={Index}", index);
                throw;
            }
        }

        public void OnLevelingFaceAppearanceChanged(int index)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingFaceAppearanceChanged(index);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling face appearance. Index={Index}", index);
                throw;
            }
        }

        public void OnLevelingScarAppearanceChanged(int index)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingScarAppearanceChanged(index);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling scar appearance. Index={Index}", index);
                throw;
            }
        }

        public void OnLevelingHairStyleAppearanceChanged(int index)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingHairStyleAppearanceChanged(index);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling hair appearance. Index={Index}", index);
                throw;
            }
        }

        public void OnLevelingHornsAppearanceChanged(int index)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingHornsAppearanceChanged(index);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling horns appearance. Index={Index}", index);
                throw;
            }
        }

        public void OnLevelingWarpaintAppearanceChanged(NetworkLevelingWarpaint levelingWarpaint)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingWarpaintAppearanceChanged(levelingWarpaint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling warpaint appearance. Index={Index}, PageNumber={PageNumber}", levelingWarpaint.Index, levelingWarpaint.PageNumber);
                throw;
            }
        }

        public void OnLevelingTattooAppearanceChanged(NetworkLevelingTattoo levelingTattoo)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingTattooAppearanceChanged(levelingTattoo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling tattoo appearance. Index={Index}, PageNumber={PageNumber}", levelingTattoo.Index, levelingTattoo.PageNumber);
                throw;
            }
        }

        public void OnLevelingBodyColorAppearanceChanged(string textureName)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingBodyColorAppearanceChanged(textureName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling body color appearance. TextureName={TextureName}", textureName);
                throw;
            }
        }

        public void OnLevelingEyesColorAppearanceChanged(string textureName)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingEyesColorAppearanceChanged(textureName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling eyes color appearance. TextureName={TextureName}", textureName);
                throw;
            }
        }

        public void OnLevelingHairColorAppearanceChanged(string textureName)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingHairColorAppearanceChanged(textureName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling hair color appearance. TextureName={TextureName}", textureName);
                throw;
            }
        }

        public void OnLevelingHornsColorAppearanceChanged(string textureName)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingHornsColorAppearanceChanged(textureName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling horns color appearance. TextureName={TextureName}", textureName);
                throw;
            }
        }

        public void OnLevelingWarpaintColorAppearanceChanged(NetworkLevelingWarpaint levelingWarpaint)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingWarpaintColorAppearanceChanged(levelingWarpaint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling warpaint appearance. TextureName={TextureName}, PageNumber={PageNumber}", levelingWarpaint.TextureName, levelingWarpaint.PageNumber);
                throw;
            }
        }

        public void OnLevelingTattooColorAppearanceChanged(NetworkLevelingTattoo levelingTattoo)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingTattooColorAppearanceChanged(levelingTattoo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling tattoo appearance. TextureName={TextureName}, PageNumber={PageNumber}", levelingTattoo.TextureName, levelingTattoo.PageNumber);
                throw;
            }
        }

        public void OnLevelingPrimaryOutfitColorAppearanceChanged(string textureName)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingPrimaryOutfitColorAppearanceChanged(textureName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling primary outfit color appearance. TextureName={TextureName}", textureName);
                throw;
            }
        }

        public void OnLevelingSecondaryOutfitColorAppearanceChanged(string textureName)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingSecondaryOutfitColorAppearanceChanged(textureName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling secondary outfit color appearance. TextureName={TextureName}", textureName);
                throw;
            }
        }

        public void OnLevelingRespecCompleted()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingRespecCompleted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while completing leveling respec");
                throw;
            }
        }

        public void OnLevelingRespecWindowShown(string unitId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingRespecWindowShown(unitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing leveling respec");
                throw;
            }
        }

        public void OnLevelingRespecLevelUp()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingRespecLevelUp();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while adding level in leveling respec");
                throw;
            }
        }

        public void OnLevelingRespecMythicLevelUp()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingRespecMythicLevelUp();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while adding mythic level in leveling respec");
                throw;
            }
        }

        public bool RequestLevelingUI(string unitId, NetworkLevelingType levelingType)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var canStartLeveling = _multiplayerActorAccessor.Current.OnRequestLevelingUI(unitId, levelingType);
                return canStartLeveling;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while requesting leveling UI. UnitId={UnitId}, Type={Type}", unitId, levelingType);
                throw;
            }
        }

        public void ForceLevelingUI(string unitId, NetworkLevelingType levelingType)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnForceLevelingUI(unitId, levelingType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while forcing leveling UI. UnitId={UnitId}, Type={Type}", unitId, levelingType);
                throw;
            }
        }

        public void OnLevelingTerminated()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingTerminated();
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error while terminating leveling");
                throw;
            }
        }

        public bool CanMakeLevelingDecisions()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var canContinue = _multiplayerActorAccessor.Current.CanMakeLevelingDecisions();
                return canContinue;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error while checking leveling decisions permissions");
                throw;
            }
        }

        public void OnWitnessLevelingPhase(NetworkLevelingPhase networkLevelingPhase)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingWitnessPhase(networkLevelingPhase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while witnessing leveling phase. Index={Index}", networkLevelingPhase.Index);
                throw;
            }
        }

        public void OnLevelingIncreaseSkillPoint(NetworkLevelingSkillPoint networkLevelingSkillPoint)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingIncreaseSkillPoint(networkLevelingSkillPoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while increasing leveling skillpoint. StatType={StatType}", networkLevelingSkillPoint.StatType);
                throw;
            }
        }

        public void OnLevelingDecreaseSkillPoint(NetworkLevelingSkillPoint networkLevelingSkillPoint)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingDecreaseSkillPoint(networkLevelingSkillPoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while decreasing leveling skillpoint. StatType={StatType}", networkLevelingSkillPoint.StatType);
                throw;
            }
        }

        public void OnLevelingIncreaseAbilityScore(NetworkLevelingAbilityScore networkLevelingAbilityScore)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingIncreaseAbilityScore(networkLevelingAbilityScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while increasing leveling ability. StatType={StatType}", networkLevelingAbilityScore.StatType);
                throw;
            }
        }

        public void OnLevelingDecreaseAbilityScore(NetworkLevelingAbilityScore networkLevelingAbilityScore)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingDecreaseAbilityScore(networkLevelingAbilityScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while decreasing leveling ability. StatType={StatType}", networkLevelingAbilityScore.StatType);
                throw;
            }
        }

        public void OnLevelingPortraitSelected(NetworkLevelingPortrait levelingPortrait)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingPortraitSelected(levelingPortrait);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while selecting leveling portrait. Name={Name}, CustomId={CustomId}, Category={Category}", levelingPortrait.Name, levelingPortrait.CustomId, levelingPortrait.Category);
                throw;
            }
        }

        public void OnLevelingVoiceSelected(NetworkLevelingVoice levelingVoice)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingVoiceSelected(levelingVoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while selecting leveling voice. Id={Id}, GenderId={GenderId}", levelingVoice.Id, levelingVoice.GenderId);
                throw;
            }
        }

        public void OnLevelingRaceSelected(string raceId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingRaceSelected(raceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while selecting leveling race. RaceId={RaceId}", raceId);
                throw;
            }
        }

        public void OnLevelingGenderSelected(string genderId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingGenderSelected(genderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while selecting leveling gender. GenderId={GenderId}", genderId);
                throw;
            }
        }

        public void OnLevelingNameChanged(string name)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingNameChanged(name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while selecting leveling name. Name={Name}", name);
                throw;
            }
        }

        public void OnLevelingAlignmentSelected(string alignmentId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingAlignmentSelected(alignmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while selecting leveling alignment. AlignmentId={AlignmentId}", alignmentId);
                throw;
            }
        }

        public void OnLevelingRacialAbilityScoreBonusChanged(NetworkLevelingSequenceDirection direction)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingRacialAbilityScoreBonusChanged(direction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling racial bonus. Direction={Direction}", direction);
                throw;
            }
        }

        public void OnLevelingBirthMonthChanged(NetworkLevelingSequenceDirection direction)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingBirthMonthChanged(direction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling birth month. Direction={Direction}", direction);
                throw;
            }
        }

        public void OnLevelingBirthDayChanged(NetworkLevelingSequenceDirection direction)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingBirthDayChanged(direction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing leveling birth day. Direction={Direction}", direction);
                throw;
            }
        }

        public void OnLevelingFeatureSelected(NetworkLevelingFeature networkLevelingFeature)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingFeatureSelected(networkLevelingFeature);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while selecting leveling feature. FeatureName={FeatureName}", networkLevelingFeature.Name);
                throw;
            }
        }

        public void OnLevelingSpellRemoved(NetworkLevelingSpell networkLevelingSpell)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingSpellRemoved(networkLevelingSpell);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while removing leveling spell. SpellName={SpellName}", networkLevelingSpell.Name);
                throw;
            }
        }

        public void OnLevelingSpellChosen(NetworkLevelingSpell networkLevelingSpell)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingSpellChosen(networkLevelingSpell);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while choosing leveling spell. SpellName={SpellName}", networkLevelingSpell.Name);
                throw;
            }
        }

        public void OnLevelingCompleted()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingCompleted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while completing leveling");
                throw;
            }
        }

        public void OnMoveActionBarSlot(NetworkActionBarSlot sourceActionBarSlot, NetworkActionBarSlot targetActionBarSlot)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnMoveActionBarSlot(sourceActionBarSlot, targetActionBarSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while moving action bar slot");
                throw;
            }
        }

        public void OnClearActionBarSlot(NetworkActionBarSlot actionBarSlot)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnClearActionBarSlot(actionBarSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while clearing action bar slot");
                throw;
            }
        }

        public bool TogglePause(bool isPaused)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var toggle = _multiplayerActorAccessor.Current.TogglePause(isPaused);
                return toggle;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while toggling pause. IsPaused={IsPaused}", isPaused);
                throw;
            }
        }

        public void OnAutoPausedByTrapDetection()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnAutoPausedByTrapDetection();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing autopause by trap detection");
                throw;
            }
        }

        public void OnLockpickInteraction(NetworkLockpickInteraction lockpickInteraction)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                var lockpickContext = _gameInteractionService.RemoteContext?.Lockpick;
                if (lockpickContext != null && string.Equals(lockpickContext.MapObjectId, lockpickInteraction.MapObject.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLockpickInteraction(lockpickInteraction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing lockpick interaction. MapObjectId={MapObjectId}", lockpickInteraction.MapObject.Id);
                throw;
            }
        }

        public void OnUnitAttackCommandStarted(NetworkUnitAttack networkUnitAttack)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnUnitAttackCommandStarted(networkUnitAttack);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing unit attack. UnitId={UnitId}, TargetUnitId={TargetUnitId}", networkUnitAttack.InitiatorUnitId, networkUnitAttack.TargetUnitId);
                throw;
            }
        }

        public void OnHandleDelayCombatTurn(string unitId, string targetUnitId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnHandleDelayCombatTurn(unitId, targetUnitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while delaying combat turn. UnitId={UnitId}, TargetUnitId={TargetUnitId}", unitId, targetUnitId);
                throw;
            }
        }

        public void OnSetUnitStealthEnabled(string unitId, bool isEnabled, bool isForced)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnSetUnitStealthEnabled(unitId, isEnabled, isForced);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing unit stealth. UnitId={UnitId}, IsEnabled={IsEnabled}", unitId, isEnabled);
                throw;
            }
        }

        public void OnGlobalMapRestOpened()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapRestOpened();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while opening rest window on global map");
                throw;
            }
        }

        public void OnRestWindowClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnRestWindowClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing rest window on global map");
                throw;
            }
        }

        public void OnGlobalMapGroupChangerOpened()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapGroupChangerOpened();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while opening group changer on global map");
                throw;
            }
        }

        public void OnGlobalMapTravelStarted(NetworkGlobalMapTravel globalMapTravel)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapTravelStarted(globalMapTravel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting travel on global map");
                throw;
            }
        }

        public bool OnGlobalMapBeforeRollTravelEncounter()
        {
            return _multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Host.IsActive;
        }

        public void OnSkipTimeOpened()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnSkipTimeOpened();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while opening skip time menu");
                throw;
            }
        }

        public void OnSkipTimeClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnSkipTimeClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing skip time menu");
                throw;
            }
        }

        public void OnSkipTimeHoursChanged(float hours)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnSkipTimeHoursChanged(hours);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing skip time value change. Hours={Hours}", hours);
                throw;
            }
        }

        public void OnSkipTimeStarted()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnSkipTimeStarted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting skip time");
                throw;
            }
        }

        public bool OnGlobalMapSelectLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var canSelect = _multiplayerActorAccessor.Current.OnGlobalMapSelectLocation(globalMapLocation);
                return canSelect;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while selecting global map location. LocationId={LocationId}, LocationName={LocationName}", globalMapLocation.Id, globalMapLocation.Name);
                throw;
            }
        }

        public void OnGlobalMapContinueTravel(NetworkGlobalMapTraveler globalMapTraveler)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapContinueTravel(globalMapTraveler);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while continuing global map travel");
                throw;
            }
        }

        public void OnGlobalMapStopTravel(NetworkGlobalMapTraveler globalMapTraveler)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapStopTravel(globalMapTraveler);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping global map travel");
                throw;
            }
        }

        public void OnGlobalMapMessageBoxShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapMessageBoxShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing global map message box");
                throw;
            }
        }

        public void OnGlobalMapLocationMessageClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapLocationMessageClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing global map message box");
                throw;
            }
        }

        public void OnGlobalMapCommonPopupShown(NetworkGlobalMapCommonPopup globalMapCommonPopup)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapCommonPopupShown(globalMapCommonPopup);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing global map common popup. Type={Type}", globalMapCommonPopup.Type);
                throw;
            }
        }

        public void OnGlobalMapCommonPopupDeclined(NetworkGlobalMapCommonPopup globalMapCommonPopup)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCommonPopupDeclined(globalMapCommonPopup);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while declining global map common popup. Type={Type}", globalMapCommonPopup.Type);
                throw;
            }
        }

        public void OnGlobalMapCommonPopupAccepted(NetworkGlobalMapCommonPopup globalMapCommonPopup)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCommonPopupAccepted(globalMapCommonPopup);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while accepting global map ingredient collection");
                throw;
            }
        }

        public void OnGlobalMapEnterLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapEnterLocation(globalMapLocation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while entering global map location. LocationId={LocationId}, LocationName={LocationName}", globalMapLocation.Id, globalMapLocation.Name);
                throw;
            }
        }

        public void OnGlobalMapEncounterMessageShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapEncounterMessageShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing global map encounter message");
                throw;
            }
        }

        public void OnGlobalMapEncounterAccepted()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapEncounterAccepted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while accepting global map encounter");
                throw;
            }
        }

        public void OnGlobalMapEncounterAvoided()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapEncounterAvoided();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while avoiding global map encounter");
                throw;
            }
        }

        public void OnGlobalMapEncounterRolled(NetworkGlobalMapEncounter globalMapEncounter)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapRandomEncounterRolled(globalMapEncounter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while rolling random encounter on global map");
                throw;
            }
        }

        public bool CanControlGlobalMap()
        {
            return _multiplayerActorAccessor.Current != null && _multiplayerActorAccessor.Host.IsActive;
        }

        public bool CanControlTacticalCombat()
        {
            return _multiplayerActorAccessor.Current != null && _multiplayerActorAccessor.Host.IsActive;
        }

        public void OnGlobalMapSkipDay()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapSkipDay();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while skipping day on global map");
                throw;
            }
        }

        public void OnGlobalMapDisposed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapDisposed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while disposing global map");
                throw;
            }
        }

        public void OnGlobalMapTravelerModeChanged(NetworkGlobalMapTravelerMode travelerMode)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapTravelerModeChanged(travelerMode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing global map traveler mode. TravelerMode={TravelerMode}", travelerMode);
                throw;
            }
        }

        public void OnGlobalMapSelectedArmyChanged(NetworkGlobalMapArmy globalMapArmy)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapSelectedArmyChanged(globalMapArmy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while selecting global map army. ArmyId={ArmyId}", globalMapArmy?.Id);
                throw;
            }
        }

        public void OnGlobalMapAutoCrusadeCombatChanged(bool isEnabled)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapAutoCrusadeCombatChanged(isEnabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing global map auto crusade combat. IsEnabled={IsEnabled}", isEnabled);
                throw;
            }
        }

        public void OnZoneLootShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnZoneLootShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing zone loot");
                throw;
            }
        }

        public void OnZoneLootClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnZoneLootClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing zone loot");
                throw;
            }
        }

        public void OnZoneLootRemoveToggleChanged(bool removeUncollectedLoot)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnZoneLootRemoveToggleChanged(removeUncollectedLoot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing zone loot remove toggle");
                throw;
            }
        }

        public void OnZoneLootCompleted()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnZoneLootCompleted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while completing zone loot");
                throw;
            }
        }

        public void OnZoneLootLeft()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnZoneLootLeft();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while completing zone loot");
                throw;
            }
        }

        public void OnZoneLootCollectorButtonsUpdated()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnZoneLootCollectorButtonsUpdated();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating zone loot collector buttons");
                throw;
            }
        }

        public void OnDialogPopupShown(NetworkDialogPopup networkDialogPopup)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnDialogPopupShown(networkDialogPopup);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing dialog popup. AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", networkDialogPopup.AreaName, networkDialogPopup.DialogName, networkDialogPopup.CueName);
                throw;
            }
        }

        public void OnDialogPopupClosed(NetworkDialogPopup networkDialogPopup)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || !_multiplayerActorAccessor.Host.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnDialogPopupClosed(networkDialogPopup);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing dialog popup. AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", networkDialogPopup.AreaName, networkDialogPopup.DialogName, networkDialogPopup.CueName);
                throw;
            }
        }

        public NetworkEquipmentSlotPosition GetEquipmentSlotPosition(ItemSlot holdingSlot)
        {
            return _gameInteractionService.GetEquipmentSlotPosition(holdingSlot);
        }

        public bool CanControlCharacterSelectionWindow()
        {
            return _multiplayerActorAccessor.Current != null && _multiplayerActorAccessor.Host.IsActive;
        }

        public void OnCharacterSelectionWindowShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnCharacterSelectionWindowShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing character selection window");
                throw;
            }
        }

        public void OnCharacterSelectionWindowAccepted()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnCharacterSelectionWindowAccepted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while accepting character selection window");
                throw;
            }
        }

        public void OnCharacterSelectionWindowClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnCharacterSelectionWindowClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing character selection window");
                throw;
            }
        }

        public void OnCharacterSelectionToggleChanged(string unitId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnCharacterSelectionToggleChanged(unitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while toggling character selection. UnitId={UnitId}", unitId);
                throw;
            }
        }

        public bool CanMakeNewGameSequenceDecisions()
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return false;
            }

            var canMakeDecisions = _multiplayerActorAccessor.Current.CanMakeNewGameSequenceDecisions();
            return canMakeDecisions;
        }

        public string GetNewGameSequenceId()
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return null;
            }

            var gameId = _multiplayerActorAccessor.Current.GetNewGameSequenceId();
            return gameId;
        }

        public void OnNewGameSequenceWitnessPhase(NetworkNewGameSequencePhase phase)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnNewGameSequenceWitnessPhase(phase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while witnessing new game sequence phase. Type={Type}", phase.Type);
                throw;
            }
        }

        public void OnNewGameDifficultyChanged(string difficulty)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnNewGameDifficultyChanged(difficulty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing new game difficulty. Difficulty={Difficulty}", difficulty);
                throw;
            }
        }
        public bool OnCreateAndEquipPolymorphInSlot(NetworkPolymorphicItem polymorphicItem)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var context = _gameInteractionService.RemoteContext?.PolymorphicItem;
                if (context != null && string.Equals(context.UnitId, polymorphicItem.UnitId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.ItemName, polymorphicItem.Item.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var shouldContinue = _multiplayerActorAccessor.Current.OnCreateAndEquipPolymorphInSlot(polymorphicItem);
                return shouldContinue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating and equiping polymorphic item. UnitId={UnitId}", polymorphicItem.UnitId);
                throw;
            }
        }

        public void OnCutsceneSkip()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnCutsceneSkip();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while skipping cutscene");
                throw;
            }
        }

        public void OnAreaTransition(NetworkAreaTransition areaTransition)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnAreaTransition(areaTransition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing area transition. AreaExitId={AreaExitId}", areaTransition.AreaExitId);
                throw;
            }
        }

        public bool OnTacticalCombatInitialization()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var canContinue = _multiplayerActorAccessor.Current.OnTacticalCombatInitialization();
                return canContinue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while initializing tactical combat");
                throw;
            }
        }

        public void OnTacticalCombatEnded()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnTacticalCombatEnded();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while ending crusade army combat");
                throw;
            }
        }

        public void OnTacticalCombatInitialized()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnTacticalCombatInitialized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing finished crusade army combat initialization");
                throw;
            }
        }

        public bool OnBeforeTacticalCombatTurnStart(int turnNumber)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var canContinue = _multiplayerActorAccessor.Current.OnBeforeTacticalCombatTurnStart(turnNumber);
                return canContinue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error before starting crusade army combat turn");
                throw;
            }
        }

        public void OnCrusadeArmyCombatTurnStarted(NetworkArmyCombatTurn armyCombatTurn)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnCrusadeArmyCombatTurnStarted(armyCombatTurn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting crusade army combat turn");
                throw;
            }
        }

        public void OnCrusadeArmyBattleResultsShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnCrusadeArmyBattleResultsShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing crusade army battle results");
                throw;
            }
        }

        public void OnCrusadeArmyBattleResultsClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnCrusadeArmyBattleResultsClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing crusade army battle results");
                throw;
            }
        }

        public void OnCrusadeArmyBattleResultsManualCombatStarted()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnCrusadeArmyBattleResultsManualCombatStarted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting manual combat via crusade army battle results");
                throw;
            }
        }

        public void OnGlobalMapCombatResultsShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapCombatResultsShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing global map combat results");
                throw;
            }
        }

        public void OnGlobalMapCombatResultsClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCombatResultsClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing global map combat results");
                throw;
            }
        }

        public void OnTacticalCombatUnitMoveToCommand(NetworkTacticalUnitMoveToCommand tacticalUnitMoveToCommand)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnTacticalCombatUnitMoveToCommand(tacticalUnitMoveToCommand);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing crusade tactical moveTo command");
                throw;
            }
        }

        public void OnTacticalCombatUnitAttackCommand(NetworkTacticalUnitAttackCommand tacticalUnitAttackCommand)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnTacticalCombatUnitAttackCommand(tacticalUnitAttackCommand);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing crusade tactical unitAttack command");
                throw;
            }
        }

        public void OnTacticalCombatUnitUseAbilityCommand(NetworkTacticalUnitUseAbilityCommand tacticalUnitUseAbilityCommand)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnTacticalCombatUnitUseAbilityCommand(tacticalUnitUseAbilityCommand);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing crusade tactical unitUseAbility command");
                throw;
            }
        }

        public bool OnTacticalCombatTotalDefenseUsed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                if (_multiplayerActorAccessor.Client.IsActive)
                {
                    return false;
                }

                var canContinue = _multiplayerActorAccessor.Host.OnTacticalCombatTotalDefenseUsed();
                return canContinue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while using crusade army combat total defense");
                throw;
            }
        }

        public bool OnTacticalCombatTurnPostponed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                if (_multiplayerActorAccessor.Client.IsActive)
                {
                    return false;
                }

                var canContinue = _multiplayerActorAccessor.Host.OnTacticalCombatTurnPostponed();
                return canContinue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while postponing crusade army combat turn");
                throw;
            }
        }

        public void OnTacticalCombatRetreat()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnTacticalCombatRetreat();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retreating from crusade army battle");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmySquadSplitRequested(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmySquadSplitRequested(sourceSquadSlot, targetSquadSlot, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while splitting squad. SourceArmyId={ArmyId}, SourcePosition={Position}, TargetArmyId={TargetArmyId}, TargetPosition={TargetPosition}, Count={Count}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position, targetSquadSlot.ArmyId, targetSquadSlot.Position, count);
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmySquadsSwitched(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmySquadsSwitched(sourceSquadSlot, targetSquadSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while splitting squad. SourceArmyId={ArmyId}, SourcePosition={Position}, TargetArmyId={TargetArmyId}, TargetPosition={TargetPosition}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position, targetSquadSlot.ArmyId, targetSquadSlot.Position);
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmySquadsMerged(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmySquadsMerged(sourceSquadSlot, targetSquadSlot, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while splitting squad. SourceArmyId={ArmyId}, SourcePosition={Position}, TargetArmyId={TargetArmyId}, TargetPosition={TargetPosition}, Count={Count}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position, targetSquadSlot.ArmyId, targetSquadSlot.Position, count);
                throw;
            }
        }

        public bool OnGlobalMapCrusadeArmySquadSplitted(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot, int count)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                if (_multiplayerActorAccessor.Client.IsActive)
                {
                    return false;
                }

                var canContinue = _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmySquadSplitted(globalMapArmySquadSlot, count);
                return canContinue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while splitting squad. ArmyId={ArmyId}, Position={Position}, Count={Count}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position, count);
                throw;
            }
        }

        public bool OnGlobalMapCrusadeArmyMergedInOne(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                if (_multiplayerActorAccessor.Client.IsActive)
                {
                    return false;
                }

                var canContinue = _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyMergedInOne(globalMapArmySquadSlot);
                return canContinue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while merging crusade squad in one. ArmyId={ArmyId}, Position={Position}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position);
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmySquadDismiss(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmySquadDismiss(globalMapArmySquadSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while dismissing crusade army squad. ArmyId={ArmyId}, Position={Position}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position);
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyInfoShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapCrusadeArmyInfoShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing crusade army info");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyMainCartClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyMainCartClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing crusade army info merge window");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyMergeCartClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapCrusadeArmyMergeCartClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing crusade army info merge window");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyRecruitCartClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyRecruitCartClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing crusade army recruit cart");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyInfoMergeShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapCrusadeArmyInfoMergeShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing crusade army info merge window");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyInfoClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyInfoClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing crusade army info");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyInfoNextMergeArmy()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyInfoNextMergeArmy();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while selecting next merge crusade army");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyInfoPrevMergeArmy()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyInfoPrevMergeArmy();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while selecting prev merge crusade army");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyMoveSquadsToMainArmy()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyMoveSquadsToMainArmy();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while moving crusade army squads to main army");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyMoveSquadsToSecondArmy()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyMoveSquadsToSecondArmy();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while moving crusade army squads to second army");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyLeaderAction(NetworkGlobalMapArmyLeader globalMapArmyLeader, NetworkGlobalMapArmyLeaderActionType armyLeaderActionType)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyLeaderAction(globalMapArmyLeader, armyLeaderActionType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while executing crusade army leader action");
                throw;
            }
        }

        public void OnGlobalMapMergeArmies()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapMergeArmies();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while merging global map armies");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyDismiss(NetworkGlobalMapArmy globalMapArmy)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyDismiss(globalMapArmy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while dismissing crusade army. ArmyId={ArmyId}", globalMapArmy.Id);
                throw;
            }
        }

        public void OnGlobalMapCreateCrusadeArmy()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCreateCrusadeArmy();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating army via crusade army info");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyCartNameChanged(NetworkGlobalMapArmy globalMapArmy)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyCartNameChanged(globalMapArmy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing main army info name");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmySetLeaderShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapCrusadeArmySetLeaderShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing army info set leader");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmySetLeaderClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapCrusadeArmySetLeaderClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing army set leader");
                throw;
            }
        }


        public void OnGlobalMapCrusadeArmySetLeaderClear()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmySetLeaderClear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while clearing leader on army info");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyInfoSetLeaderRecruit()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmySetLeaderRecruit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while opening leader recruitment on army info");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyBuyLeaderShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapCrusadeArmyBuyLeaderShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing buy leader screen");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyBuyLeaderClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapCrusadeArmyBuyLeaderClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing buy leader screen");
                throw;
            }
        }

        public void OnGlobalMapRecruitmentShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapRecruitmentShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing global map recruitment");
                throw;
            }
        }

        public void OnGlobalMapRecruitmentClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapRecruitmentClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing global map recruitment");
                throw;
            }
        }

        public void OnGlobalMapRecruitmentMercReroll()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapRecruitmentMercReroll();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while rerolling global map recruitment mercs");
                throw;
            }
        }

        public void OnGlobalMapRecruitmentNextArmy()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapRecruitmentNextArmy();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while setting next global map recruitment army");
                throw;
            }
        }

        public void OnGlobalMapRecruitmentPrevArmy()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapRecruitmentPrevArmy();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while setting prev global map recruitment army");
                throw;
            }
        }

        public void OnGlobalMapRecruitmentSlotsRerolled()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapRecruitmentSlotsRerolled();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while handling rerolled recruitment slots");
                throw;
            }
        }

        public void OnGlobalMapRecruitmentBuyResources(NetworkGlobalMapResourceOrder globalMapResourceOrder)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapRecruitmentBuyResources(globalMapResourceOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while buying global map resources");
                throw;
            }
        }

        public void OnGlobalMapRecruitmentBuyUnits(NetworkGlobalMapUnitRecruitmentOrder globalMapUnitRecruitmentOrder)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapRecruitmentBuyUnits(globalMapUnitRecruitmentOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while buying global map units");
                throw;
            }
        }

        public void OnGlobalMapMagicSpellUsed(NetworkGlobalMapMagicSpell globalMagicSpell)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapMagicSpellUsed(globalMagicSpell);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while using global map magic spell. SpellId={SpellId}, SpellName={SpellName}, TargetArmies={TargetArmies}, LocationId={LocationId}", globalMagicSpell.Id, globalMagicSpell.Name, globalMagicSpell.TargetArmies, globalMagicSpell.Location?.Id);
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyLeaderLevelingShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapCrusadeArmyLeaderLevelingShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing crusade army leader leveling screen");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyLeaderLevelingClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyLeaderLevelingClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing crusade army leader leveling screen");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyLeaderLevelingConfirmed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyLeaderLevelingConfirmed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while confirming crusade army leader leveling screen");
                throw;
            }
        }

        public void OnGlobalMapCrusadeArmyLeaderLevelingSkillSelected(string skillId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapCrusadeArmyLeaderLevelingSkillSelected(skillId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while selecting crusade army leader leveling skill. SkillId={SkillId}", skillId);
                throw;
            }
        }

        public void OnUnitDeath(string unitId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnUnitDeath(unitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing combat unit death. UnitId={UnitId}", unitId);
                throw;
            }
        }

        public void OnTrapDisarmRolled(NetworkTrapDisarm trapDisarm)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnTrapDisarmRolled(trapDisarm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing trap disarm roll. TrapId={TrapId}, UnitId={UnitId}, Roll={Roll}", trapDisarm.MapObject.Id, trapDisarm.UnitId, trapDisarm.Roll);
                throw;
            }
        }

        public void OnUnitAutoUseAbilityChanged(NetworkAutoUseAbility networkAutoUseAbility)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnUnitAutoUseAbilityChanged(networkAutoUseAbility);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing unit autouse ability. UnitId={UnitId}, AbilityName={AbilityName}", networkAutoUseAbility.UnitId, networkAutoUseAbility.Ability?.Name);
                throw;
            }
        }

        public void OnCopyInventoryItem(NetworkItemCopy itemCopy)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnCopyInventoryItem(itemCopy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while copying inventory item. UnitId={UnitId}, ItemName={ItemName}", itemCopy.UnitId, itemCopy.Item.Name);
                throw;
            }
        }

        public void CloseMultiplayerLobbyWindow()
        {
            if (_lobbyWindow != null && _lobbyWindow.IsVisible)
            {
                _logger.LogInformation("Closing lobby window");
                _lobbyWindow.Close();
            }
        }

        private void ShowMultiplayerLobbyWindow()
        {
            _logger.LogInformation("Opening lobby window");
            _lobbyWindow.Show();
        }

        private void ShowMultiplayerWindow()
        {
            _logger.LogInformation("Show Multiplayer window");
            _multiplayerWindow.Show(true);
        }

        private void OnMultiplayerHostLobbyCharacterOwnerChanged(NetworkCharacter character, NetworkPlayer player)
        {
            _logger.LogInformation("OnMultiplayerHostLobbyCharacterOwnerChanged. CharacterId={CharacterId}, PlayerId={PlayerId}", character.UnitId, player.Id);
            _multiplayerActorAccessor.Host.ChangeCharacterOwner(character, player);
        }

        private void OnMultiplayerClientCharacterOwnerChanged(NetworkCharacter character)
        {
            _logger.LogInformation("OnMultiplayerClientCharacterOwnerChanged. CharacterId={CharacterId}, PlayerId={PlayerId}", character.Name, character.Owner.Id);
            _lobbyWindowController.UpdateCharacterOwnerDropdown(character, silent: true);
        }
    }
}
