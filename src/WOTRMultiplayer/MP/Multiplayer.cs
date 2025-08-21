using System;
using System.Collections.Generic;
using System.Reflection;
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
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Leveling;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Movement;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Vendor;
using WOTRMultiplayer.UI;
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
            IValueGenerator valueGenerator)
        {
            _logger = logger;
            Factory = uiFactory;
            _multiplayerActorAccessor = multiplayerActorAccessor;
            _lobbyWindowController = lobbyWindowController;
            _gameInteractionService = gameInteractionService;
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

            return true;
        }

        public void TerminateMultiplayer()
        {
            _logger.LogInformation("Disposing both multiplayer host/client");
            _multiplayerActorAccessor.Host.Reset();
            _multiplayerActorAccessor.Client.Reset();
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
            _lobbyWindow = Factory.InitializeEscMenuLobbyWindow(context, ShowEscMenuMultiplayerLobby);

            _lobbyWindow.GetGameConnectivity = _multiplayerActorAccessor.Current.GetGameConnectivity;
            _lobbyWindow.GetPlayers = _multiplayerActorAccessor.Current.GetPlayers;
            _lobbyWindow.GetCharacters = _multiplayerActorAccessor.Current.GetCharacters;
            _lobbyWindow.GetIsHost = () => _multiplayerActorAccessor.Host.IsActive;

            _lobbyWindow.AssignLobbyController(_lobbyWindowController);

            _lobbyWindowController.OnCharacterOwnerChanged = OnLobbyCharacterOwnerChanged;
        }

        public void MoveNonCombatCharacter(NetworkCharacterMove move)
        {
            try
            {

                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.MoveNonCombatCharacter(move);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public string GetMultiplayerOwnerName(string unitId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return null;
                }

                return _multiplayerActorAccessor.Current.GetMultiplayerOwnerName(unitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{methodName}", MethodBase.GetCurrentMethod().Name);
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

        public bool OnStartGameMode(GameModeType type)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }


                var canContinue = _multiplayerActorAccessor.Current.OnStartGameMode(type);
                return canContinue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool OnStopGameMode(GameModeType type)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var canContinue = _multiplayerActorAccessor.Current.OnStopGameMode(type);
                return canContinue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{methodName}", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public bool CanLeaveArea()
        {
            return !_multiplayerActorAccessor.Client.IsActive;
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
                _logger.LogError(ex, "{methodName}", MethodBase.GetCurrentMethod().Name);
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
                _logger.LogError(ex, "{methodName}", MethodBase.GetCurrentMethod().Name);
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
                _logger.LogError(ex, "{methodName}", MethodBase.GetCurrentMethod().Name);
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

                return _multiplayerActorAccessor.Current.OnBeforeEndTurn(unitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while ending turn");
                throw;
            }
        }

        public void ForceLoadGame(SaveInfo saveInfo)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                // extra validation is not required since everything is already validated by the game
                var savePath = saveInfo.FolderName;
                _logger.LogInformation("Force load game. SaveLocation={SaveLocation}, GameId={GameId}", savePath, saveInfo.GameId);
                _multiplayerActorAccessor.Current.ForceLoadGame(savePath, saveInfo.GameId);
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

        public void OnClickGround(NetworkClick click)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnClickGround(click);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while clicking on ground. WorldPosition={WorldPosition}", click?.WorldPosition);
                throw;
            }
        }

        public void OnClickMapObject(NetworkClick click)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnClickMapObject(click);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while clicking map object. MapObjectId={MapObjectId}", click?.MapObjectId);
                throw;
            }
        }

        public void OnAbilityUse(NetworkAbility ability)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnAbilityUse(ability);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while using ability. AbilityName={AbilityName}", ability?.Name);
                throw;
            }
        }

        public void OnToggleActivatableAbility(NetworkActivatableAbility activatableAbilityUse)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnToggleActivatableAbility(activatableAbilityUse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while toggling activatable ability. AbilityId={AbilityId}", activatableAbilityUse?.Id);
                throw;
            }
        }

        public NetworkActionsState GetActionsState()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return null;
                }

                var actionsState = _gameInteractionService.GetActionsState();
                return actionsState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting combat actions state");
                throw;
            }
        }

        public bool CanLootUnit(string initiatorUnitId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return true;
                }

                var isControlledByLocalPlayer = _multiplayerActorAccessor.Current.IsControlledByLocalPlayer(initiatorUnitId);
                return isControlledByLocalPlayer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking if unit is allowed to be looted. InitiatorUnitId={InitiatorUnitId}", initiatorUnitId);
                throw;
            }
        }

        public void OnLootContainer(NetworkLootContainer container)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLootContainer(container);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while looting container. ContainerId={ContainerId}", container?.Id);
                throw;
            }
        }

        public void OnDropItem(NetworkDropItem dropItem)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                var context = _gameInteractionService.RemoteContext?.DropItem;
                if (context != null && string.Equals(context.UnitId, dropItem.OwnerEntityId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.ItemId, dropItem.Item.UniqueId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnDropItem(dropItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while dropping item. ItemId={ItemId}", dropItem?.Item?.UniqueId);
                throw;
            }
        }

        public void OnChangeActiveHandEquipmentSet(NetworkActiveHandEquipmentSet set)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                var context = _gameInteractionService.RemoteContext?.HandEquipment;
                if (context != null
                    && context.Index == set.Index
                    && string.Equals(context.UnitId, set.UnitId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnChangeActiveHandEquipmentSet(set);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while changing active hand equipment set. UnitId={UnitId}", set?.UnitId);
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

                if (networkOvertip.RequiresEveryoneToMoveMove)
                {
                    _gameInteractionService.SetGroundMoveEveryone();
                }

                _multiplayerActorAccessor.Current.OnInteractWithMapObjectOvertip(networkOvertip);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while interacting with map object overtip. MapObjectId={MapObjectId}", networkOvertip?.MapObject?.Id);
                throw;
            }
        }

        public void ResetExecutionContext()
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _gameInteractionService.RemoteContext?.Dispose();
        }

        public bool ShouldGroundHandlerMoveAllUnitsToPoint()
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return false;
            }

            var context = _gameInteractionService.RemoteContext?.UnitsMovement;
            return context != null && context.ShouldMoveEveryone;
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

        public void OnPerceptionCheck(NetworkPerceptionCheck check)
        {
            try
            {
                if (!_multiplayerActorAccessor.Host.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnPerceptionCheck(check);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing perception check. UnitId={UnitId}", check?.UnitId);
                throw;
            }
        }

        public bool CanMakePerceptionCheck(string unitId, string mapObjectId)
        {
            if (!_multiplayerActorAccessor.Client.IsActive)
            {
                return true;
            }

            var perceptionCheck = _gameInteractionService.RemoteContext?.PerceptionCheck;
            if (perceptionCheck == null)
            {
                return false;
            }

            return string.Equals(unitId, perceptionCheck.UnitId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(mapObjectId, perceptionCheck.MapObjectId, StringComparison.OrdinalIgnoreCase);
        }


        public bool OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck check)
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

                _multiplayerActorAccessor.Host.OnInspectionKnowledgeCheck(check);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing inspection knowledge check. InitiatorUnitId={InitiatorUnitId}", check?.InitiatorUnitId);
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
                if (_multiplayerActorAccessor.Client.IsActive)
                {
                    _gameInteractionService.ShowWarningNotification(UIStringConsts.GameNotifications.TryingToSetUpCampAsAClient);
                    return false;
                }

                if (!_multiplayerActorAccessor.Host.IsActive)
                {
                    return true;
                }

                var canContinue = _multiplayerActorAccessor.Host.OnSpawnCampPlace(position);
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

        public void OnCampingUnitsRoleChanged(List<NetworkCampingRole> roles)
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

                _multiplayerActorAccessor.Host.OnCampingUnitsRoleChanged(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing camping units role change");
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

                if (_multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnStartRest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting camp rest");
                throw;
            }
        }

        public bool CanUseCampingUI()
        {
            return _multiplayerActorAccessor.Current != null && _multiplayerActorAccessor.Host.IsActive;
        }

        public void OnBeforeTryRollRandomEncounter()
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

                _multiplayerActorAccessor.Client.OnBeforeTryRollRandomEncounter();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error before rolling random encounter");
                throw;
            }
        }

        public void OnAfterTryRollRandomEncounter()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                if (_multiplayerActorAccessor.Host.IsActive)
                {
                    _multiplayerActorAccessor.Host.OnAfterTryRollRandomEncounter();
                    return;
                }

                if (_gameInteractionService.RemoteContext?.RandomEncounter != null)
                {
                    _gameInteractionService.RemoteContext.RandomEncounter = null;
                    _gameInteractionService.UpdateIsInCombatStatus();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error after rolling random encounter", MethodBase.GetCurrentMethod().Name);
                throw;
            }
        }

        public int? GetNextRestBanter(int minInclusive, int maxExclusive)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return null;
                }

                var banterSeed = _multiplayerActorAccessor.Current.RestBanterSeed;
                var nextBanter = ValueGenerator.Range(banterSeed, minInclusive, maxExclusive);
                _logger.LogInformation("Next rest banter has been selected. Seed={Seed}, Index={Index}", banterSeed, nextBanter);
                return nextBanter;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting next rest banter");
                throw;
            }
        }

        public void OnInterrupRestBanterBark(NetworkRestBanter networkBanter)
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnInterrupRestBanterBark(networkBanter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while interrupting rest banter bark. BanterKey={BanterKey}", networkBanter.Key);
                throw;
            }
        }

        public NetworkAIAction OnAfterAISelectedAction(NetworkAIAction action)
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return null;
                }

                var possibleOverride = _multiplayerActorAccessor.Current.OnAfterAISelectedAction(action);
                return possibleOverride;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error after AI selected action. AIUnitId={AIUnitId}", action?.UnitId);
                throw;
            }
        }

        public void OnTransferVendorItem(NetworkVendorItemTransfer transfer)
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return;
                }

                var vendorItemTransfer = _gameInteractionService.RemoteContext?.VendorItemTransfer;
                if (vendorItemTransfer != null && string.Equals(vendorItemTransfer.ItemId, transfer.Item.UniqueId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnTransferVendorItem(transfer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while transferring vendor item. ItemId={ItemId}", transfer?.Item?.UniqueId);
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

        public void OnMemorizeSpell(NetworkSpellSlot slot)
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnMemorizeSpell(slot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while memorizing spell. UnitId={UnitId}", slot?.UnitId);
                throw;
            }
        }

        public void OnForgetSpell(NetworkSpellSlot slot)
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnForgetSpell(slot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while forgetting spell. UnitId={UnitId}", slot?.UnitId);
                throw;
            }
        }

        public void OnLevelingClassArchetypeSelected(string archetypeId)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLevelingClassArchetypeSelected(archetypeId);
        }

        public void OnLevelingClassSelected(string classId)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLevelingClassSelected(classId);
        }

        public bool RequestLevelingUI(string unitId)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return true;
            }

            if (_multiplayerActorAccessor.Client.IsActive)
            {
                var canContinue = _multiplayerActorAccessor.Client.RequestLevelingUI(unitId);
                return canContinue;
            }

            _multiplayerActorAccessor.Host.OnCharacterLevelingStarted(unitId);
            return true;
        }

        public void OnLevelingTerminated()
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLevelingTerminated();
        }

        public bool CanMakeLevelingDecisions()
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return true;
            }

            var canContinue = _multiplayerActorAccessor.Current.CanMakeLevelingDecisions();
            return canContinue;
        }

        public void OnWitnessLevelingPhase(NetworkLevelingPhase phase)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLevelingWitnessPhase(phase);
        }

        public void OnLevelingIncreaseSkillPoint(NetworkLevelingSkillPoint skill)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLevelingIncreaseSkillPoint(skill);
        }

        public void OnLevelingDecreaseSkillPoint(NetworkLevelingSkillPoint skill)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLevelingDecreaseSkillPoint(skill);
        }

        public void OnLevelingIncreaseAbilityScore(NetworkLevelingAbilityScore abilityScore)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLevelingIncreaseAbilityScore(abilityScore);
        }

        public void OnLevelingDecreaseAbilityScore(NetworkLevelingAbilityScore abilityScore)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLevelingDecreaseAbilityScore(abilityScore);
        }

        public void OnLevelingFeatureSelected(NetworkLevelingFeature feature)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLevelingFeatureSelected(feature);
        }

        public void OnLevelingSpellRemoved(NetworkLevelingSpell spell)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLevelingSpellRemoved(spell);
        }

        public void OnLevelingSpellChosen(NetworkLevelingSpell spell)
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLevelingSpellChosen(spell);
        }

        public void OnLevelingCompleted()
        {
            if (_multiplayerActorAccessor.Current == null)
            {
                return;
            }

            _multiplayerActorAccessor.Current.OnLevelingCompleted();
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
            _logger.LogInformation("OnLobbyCharacterOwnerChanged. CharacterIndex={CharacterIndex}, PlayerIndex={PlayerIndex}", characterIndex, playerIndex);
            _multiplayerActorAccessor.Host.ChangeCharacterOwner(characterIndex, playerIndex);
        }
    }
}
