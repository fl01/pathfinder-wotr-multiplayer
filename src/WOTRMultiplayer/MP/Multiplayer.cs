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
using WOTRMultiplayer.MP.Entities.ActionBar;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.GlobalMap;
using WOTRMultiplayer.MP.Entities.Inspect;
using WOTRMultiplayer.MP.Entities.Leveling;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Movement;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Vendor;
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
                _logger.LogError(ex, "Error while starting game mode. GameMode={GameMode}", type);
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
                _logger.LogError(ex, "Error while stopping game mode. GameMode={GameMode}", type);
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
                _logger.LogError(ex, "Error after cue show");
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
                _logger.LogError(ex, "Error before selecting dialog answer");
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

        public void OnAbilityUse(NetworkAbility networkAbility)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnAbilityUse(networkAbility);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while using ability. AbilityName={AbilityName}", networkAbility?.Name);
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
                _logger.LogError(ex, "Error while processing perception check. UnitId={UnitId}", networkPerceptionCheck?.UnitId);
                throw;
            }
        }

        public bool CanMakePerceptionCheck(string unitId, string mapObjectId)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking perception check permissions. UnitId={UnitId}", unitId);
                throw;
            }
        }


        public bool OnInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck networkInspectionKnowledgeCheck)
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

                _multiplayerActorAccessor.Host.OnInspectionKnowledgeCheck(networkInspectionKnowledgeCheck);
                return true;
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

                var banterSeed = _multiplayerActorAccessor.Current.SessionSeed;
                var nextBanter = ValueGenerator.Range(Random.SeedLifetime.Persistent, banterSeed, minInclusive, maxExclusive);
                _logger.LogInformation("Next rest banter has been selected. Seed={Seed}, Index={Index}", banterSeed, nextBanter);
                return nextBanter;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting next rest banter");
                throw;
            }
        }

        public int GetCombatSeed()
        {
            // SessionSeed is a fallback in case we rely on combat seed outside of the combat.
            // there is only 1 known case as of now: attacking someone with MirrorImage buff when combat has not been started yet
            // fallback will not provide true randomness as the same unit would lose the same amount of mirror images each time it's attacked outside of the combat
            // but it's extremely rare to be in this situation anyway
            var seed = _multiplayerActorAccessor.Current?.CombatSeed ?? _multiplayerActorAccessor.Current?.SessionSeed;
            if (!seed.HasValue)
            {
                _logger.LogError("Neither CombatSeed nor SessionSeed is available. Those values should not be requested outside of the MP session");
                return -1;
            }

            return seed.Value;
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

        public void OnMemorizeSpell(NetworkSpellSlot networkSpellSlot)
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnMemorizeSpell(networkSpellSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while memorizing spell. UnitId={UnitId}", networkSpellSlot?.UnitId);
                throw;
            }
        }

        public void OnForgetSpell(NetworkSpellSlot networkSpellSlot)
        {
            try
            {
                if (_multiplayerActorAccessor == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnForgetSpell(networkSpellSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while forgetting spell. UnitId={UnitId}", networkSpellSlot?.UnitId);
                throw;
            }
        }

        public void OnLevelingClassArchetypeSelected(string archetypeId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingClassArchetypeSelected(archetypeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing leveling class archetype selection. ArchetypeId={ArchetypeId}", archetypeId);
                throw;
            }
        }

        public void OnLevelingClassSelected(string classId)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnLevelingClassSelected(classId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing leveling class selection. ClassId={ClassId}", classId);
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

                var lockpickContext = _gameInteractionService.RemoteContext?.LockpickContext;
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
                _logger.LogError(ex, "Error while processing unit attack. UnitId={UnitId}, TargetUnitId={TargetUnitId}", networkUnitAttack.ExecutorUnitId, networkUnitAttack.TargetUnitId);
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

                _multiplayerActorAccessor.Host.OnGlobalMapRestMenuOpened();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while opening rest menu on global map");
                throw;
            }
        }

        public void OnGlobalMapStartTravel(NetworkGlobalMapLocation destination)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapStartTravel(destination);
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

        public void OnGlobalMapContinueTravel(NetworkGlobalMapState globalMapState)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapContinueTravel(globalMapState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while continuing global map travel");
                throw;
            }
        }

        public void OnGlobalMapStopTravel(NetworkGlobalMapState globalMapState)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapStopTravel(globalMapState);
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

        public void OnGlobalMapMessageBoxClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapMessageBoxClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing global map message box");
                throw;
            }
        }

        public void OnGlobalMapIngredientCollectionShown()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapIngredientCollectionShown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while showing global map ingredient collection");
                throw;
            }
        }

        public void OnGlobalMapIngredientCollectionClosed()
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnGlobalMapIngredientCollectionClosed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while closing global map ingredient collection");
                throw;
            }
        }

        public void OnGlobalMapIngredientCollectionAccepted(NetworkGlobalMapLocation globalMapLocation)
        {
            try
            {
                if (_multiplayerActorAccessor.Current == null || _multiplayerActorAccessor.Client.IsActive)
                {
                    return;
                }

                _multiplayerActorAccessor.Host.OnGlobalMapIngredientCollectionAccepted(globalMapLocation);
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

        public bool CanNavigateOnGlobalMap()
        {
            return _multiplayerActorAccessor.Host.IsActive;
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
                if (_multiplayerActorAccessor.Current == null)
                {
                    return;
                }

                _multiplayerActorAccessor.Current.OnZoneLootCompleted();
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
