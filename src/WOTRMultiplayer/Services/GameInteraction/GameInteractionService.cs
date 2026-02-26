using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Clicks;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.Controllers.Rest;
using Kingmaker.Controllers.Rest.State;
using Kingmaker.Controllers.Units;
using Kingmaker.Craft;
using Kingmaker.DLC;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.Inspect;
using Kingmaker.Items;
using Kingmaker.Items.Parts;
using Kingmaker.Items.Slots;
using Kingmaker.Modding;
using Kingmaker.Pathfinding;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.Settings;
using Kingmaker.TurnBasedMode;
using Kingmaker.UI;
using Kingmaker.UI._ConsoleUI.Overtips;
using Kingmaker.UI.CharSelect;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._PCView.MapIslands;
using Kingmaker.UI.MVVM._PCView.NewGame.Story;
using Kingmaker.UI.MVVM._PCView.Rest;
using Kingmaker.UI.MVVM._PCView.Settings.Entities.Difficulty;
using Kingmaker.UI.MVVM._PCView.Transition;
using Kingmaker.UI.MVVM._VM.Lockpick;
using Kingmaker.UI.MVVM._VM.MapIslands;
using Kingmaker.UI.MVVM._VM.NewGame;
using Kingmaker.UI.MVVM._VM.Settings.Entities;
using Kingmaker.UI.Selection;
using Kingmaker.UI.SettingsUI;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.View.MapObjects;
using Kingmaker.View.MapObjects.Traps;
using Kingmaker.View.MapObjects.Traps.Simple;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityModManagerNet;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.GameInteraction.CombatLog;
using WOTRMultiplayer.Abstractions.GameInteraction.CombatLog.Tooltips;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.ActionBar;
using WOTRMultiplayer.Entities.Area;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Content;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Entities.NewGame;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Entities.SpellbookManagement;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Entities.Vendor;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Services.GameInteraction.Contexts;
using WOTRMultiplayer.Services.Settings;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class GameInteractionService : IGameInteractionService
    {
        private readonly AsyncLocal<RemoteExecutionContext> _networkExecutionContext = new();

        private readonly ILogger<GameInteractionService> _logger;
        private readonly IUIAccessor _uiAccessor;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IEquipmentDefinitions _equipmentDefinitions;
        private readonly IPlayerNotificationService _playerNotificationService;
        private readonly IUISyncCountersService _uiSyncCountersService;
        private readonly IGameStateLookupService _gameStateLookupService;

        public RemoteExecutionContext RemoteContext => _networkExecutionContext.Value;

        public GameModeType CurrentGameMode => Game.Instance.CurrentMode;

        public bool IsPaused => Game.Instance.IsPaused;

        public bool IsCapitalPartyMode => Game.Instance.Player.CapitalPartyMode;

        public GameInteractionService(
            ILogger<GameInteractionService> logger,
            IUIAccessor uiAccessor,
            IMainThreadAccessor mainThreadAccessor,
            IEquipmentDefinitions equipmentDefinitions,
            IPlayerNotificationService playerNotificationService,
            IUISyncCountersService uiSyncCountersService,
            IGameStateLookupService gameStateLookupService)
        {
            _logger = logger;
            _uiAccessor = uiAccessor;
            _mainThreadAccessor = mainThreadAccessor;
            _equipmentDefinitions = equipmentDefinitions;
            _playerNotificationService = playerNotificationService;
            _uiSyncCountersService = uiSyncCountersService;
            _gameStateLookupService = gameStateLookupService;
        }

        public NetworkCampingState GetCampigState()
        {
            var camping = Game.Instance.Player.Camping;
            var state = new NetworkCampingState
            {
                PotionBlueprintRecipeId = camping.SelectedPotion?.Item.AssetGuid.ToString(),
                CookingBlueprintRecipeId = camping.CookingRecipe?.AssetGuid.ToString(),
                ScrollBlueprintRecipeId = camping.SelectedScroll?.Item.AssetGuid.ToString(),
                AutotuneIterationsStatus = camping.AutotuneRestIterations,
                IterationsCount = camping.RestIterationsCount
            };

            return state;
        }

        public void InteractWithOvertip(NetworkOvertip networkOvertip)
        {
            _mainThreadAccessor.Post(() =>
            {
                var units = networkOvertip.Units.Select(_gameStateLookupService.GetUnitEntity).ToList();
                using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(units);
                context.Overtip = new OvertipInteractionContext { MapObjectId = networkOvertip.MapObject.Id };

                if (networkOvertip.VectorPath != null && networkOvertip.VectorPath.Any())
                {
                    if (PathVisualizer.Instance != null)
                    {
                        PathVisualizer.Instance.m_CurrentPath = new ForcedPath([.. networkOvertip.VectorPath.Select(x => x.ToUnityVector3())]);
                        PathVisualizer.Instance.m_CurrentPath.Claim(PathVisualizer.Instance);
                        _logger.LogInformation("VectorPath for overtip interaction has been set. Path={Path}", networkOvertip.VectorPath);
                    }
                }

                var mapObject = _gameStateLookupService.GetMapObject(networkOvertip.MapObject.Id);
                if (mapObject == null)
                {
                    _logger.LogError("Unable to perform overtip interaction with missing map object. MapObjectId={MapObjectId}", networkOvertip.MapObject.Id);
                    return;
                }

                if (mapObject.Interactions.Count == 0)
                {
                    var view = FindOvertipForObject(mapObject);
                    // game doesn't render overtips if it's not visible on your screen
                    var areaTransitionComponent = mapObject.View.GetComponent<AreaTransition>();
                    if (view == null && areaTransitionComponent != null)
                    {
                        var transitionOvertipVM = new EntityOvertipVM(mapObject, OvertipsView.Instance.ViewModel);
                        OvertipsView.Instance.AddMapObject(transitionOvertipVM);
                        view = FindOvertipForObject(mapObject);
                    }

                    if (view is AreaTransitionOvertipView areaTransitionOvertip)
                    {
                        _logger.LogInformation("Interacting with {overtipType}. MapObjectId={MapObjectId}", nameof(AreaTransitionOvertipView), mapObject.UniqueId);
                        areaTransitionOvertip.OnClick();
                        return;
                    }

                    _logger.LogWarning("Unable to interact with target object. MapObjectId={MapObjectId}", mapObject.UniqueId);
                    return;
                }

                _logger.LogInformation("Interacting with object via OvertipVM", mapObject.UniqueId);
                // overtips are created by game on demand, but we need to make sure overtip exists
                var overtipVM = new EntityOvertipVM(mapObject, OvertipsView.Instance.ViewModel);
                // TODO: maybe get exact interactionpart
                overtipVM.Interact(mapObject.Interactions.FirstOrDefault());
                overtipVM.Dispose();
            });
        }

        public string GetSaveGamePath()
        {
            var path = Game.Instance.SaveManager.SavePath;
            return path;
        }

        public SaveInfo LoadSave(string path)
        {
            var save = Game.Instance.SaveManager.LoadZipSave(path);
            return save;
        }

        public void LeaveArea(NetworkAreaTransition networkAreaTransition)
        {
            _mainThreadAccessor.Post(() =>
            {
                var currentArea = Game.Instance.CurrentlyLoadedArea;
                if (!string.Equals(currentArea?.AssetGuid.ToString(), networkAreaTransition.From.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Current area doesn't match area transition. AreaExitId={AreaExitId}, FromAreaId={FromAreaId}, FromAreaName={FromAreaName}, CurrentAreaId={CurrentAreaId}, CurrentAreaName={CurrentAreaName}", networkAreaTransition.AreaExitId, networkAreaTransition.From.Id, networkAreaTransition.From.Name, currentArea?.AssetGuid.ToString(), currentArea?.name);
                    return;
                }

                var allTransitions = Game.Instance.State.MapObjects.All.Select(o => o.View.GetComponent<AreaTransition>()).Where(t => t != null).ToList();
                var transition = allTransitions.FirstOrDefault(x => string.Equals(x.GetComponent<MapObjectView>().UniqueId, networkAreaTransition.AreaExitId, StringComparison.OrdinalIgnoreCase));
                var areaTransition = transition?.GetComponent<MapObjectView>()?.Data.Get<AreaTransitionPart>();
                if (areaTransition == null)
                {
                    _logger.LogError("Unable to find requested area transition. TransitionsCount={TransitionsCount}, AreaExitId={AreaExitId}", allTransitions.Count, networkAreaTransition.AreaExitId);
                    return;
                }

                if (networkAreaTransition.IsActionsTransition)
                {
                    var action = areaTransition.Blueprint.Actions.FirstOrDefault(a => a.Condition == null || a.Condition.Check(null));
                    action.Actions.Run();
                    _logger.LogInformation("Area transition has been executed via blueprint actions");
                    return;
                }

                // AreaTransitionGroupCommand.ExecuteTransition
                if (Game.Instance.State.LoadedAreaState.Encounter == null && areaTransition.AreaEnterPoint.Area.IsGlobalMap)
                {
                    BlueprintGlobalMap globalMap = BlueprintRoot.Instance.GlobalMap.GetGlobalMap(areaTransition.AreaEnterPoint);
                    if (globalMap != null)
                    {
                        Game.Instance.Player.GetGlobalMap(globalMap).Player.AreaReturnPoint = areaTransition.GetEnterPointToReturnTo();
                    }
                }

                var targetPoint = areaTransition.AreaEnterPoint;
                _logger.LogInformation("Leaving area. AreaExitId={AreaExitId}, TargetAreaName={TargetAreaName}", networkAreaTransition.AreaExitId, targetPoint.Area?.AreaName);
                EventBus.RaiseEvent<IPartyLeaveAreaHandler>(x => x.HandlePartyLeaveArea(Game.Instance.CurrentlyLoadedArea, targetPoint, areaTransition));
                Game.Instance.LoadArea(targetPoint, areaTransition.Settings.AutoSaveMode, null);
            });

        }

        public void SetPause(bool isPaused)
        {
            _logger.LogInformation("Pause game. RequestedIsPaused={IsPaused}, GameIsPaused={GameIsPaused}", isPaused, Game.Instance.IsPaused);
            if (Game.Instance.IsPaused == isPaused)
            {
                return;
            }

            if (isPaused)
            {
                Game.Instance.IsPaused = true;
                return;
            }

            Game.Instance.m_WillBePaused = false;
            Game.Instance.PauseOnLoadPendingTicks = 0;
            Game.Instance.IsPaused = false;
        }

        public List<NetworkCharacter> GetPartyPlayers()
        {
            var partyCharacters = Game.Instance.Player.Party
                .Select(x => new NetworkCharacter
                {
                    Name = x.CharacterName,
                    Portrait = x.Portrait.SmallPortrait.name,
                    UnitId = x.UniqueId
                })
                .ToList();

            return partyCharacters;
        }

        public bool IsUnitInParty(string unitId)
        {
            var groupToSearch = _gameStateLookupService.GetActualParty();
            var unit = groupToSearch.FirstOrDefault(p => string.Equals(p.UniqueId, unitId, StringComparison.OrdinalIgnoreCase));
            return unit != null;
        }

        public string QuickLoadGame(string savePath)
        {
            var save = LoadSave(savePath);
            if (save == null)
            {
                _logger.LogError("Unable to quick load save. Path={Path}", savePath);
                return null;
            }

            _mainThreadAccessor.Post(() =>
            {
                _uiAccessor.CloseAllWindows();
                Game.Instance.LoadGame(save);
            });
            return save.GameId;
        }

        public string LoadGameFromMainMenu(string savePath)
        {
            var save = LoadSave(savePath);
            if (save == null)
            {
                _logger.LogError("Unable to load save. Path={Path}", savePath);
                return null;
            }

            _mainThreadAccessor.Post(() =>
            {
                Game.Instance.RootUiContext.MainMenuVM.EnterLoadGame(save);
            });

            return save.GameId;
        }

        public void StartNewGameSequence(string mainCharacterId, Action onBack, Action onStart, Action<NetworkCharacter> onCharacterCreated)
        {
            _mainThreadAccessor.Post(() =>
            {
                var mainMenuVM = Game.Instance.RootUiContext.MainMenuVM;
                void PreviousStep()
                {
                    mainMenuVM.DisposeNewGame();
                    mainMenuVM.UpdateSoundState();

                    onBack?.Invoke();
                }

                void OnCharacterCreated()
                {
                    var character = new NetworkCharacter
                    {
                        Name = mainMenuVM.m_ChargenUnit.CharacterName,
                        UnitId = mainMenuVM.m_ChargenUnit.UniqueId
                    };
                    onCharacterCreated?.Invoke(character);
                    mainMenuVM.EnterNewGame();
                }

                void NextStep()
                {
                    onStart?.Invoke();

                    mainMenuVM.NewGameVM.SetCommonVisible(value: false);
                    if (Game.ImportSave != null)
                    {
                        mainMenuVM.EnterNewGame();
                    }
                    else
                    {
                        if (mainMenuVM.m_ChargenUnit == null || mainMenuVM.m_Campaign != Game.NewGamePreset.Campaign)
                        {
                            mainMenuVM.m_Campaign = Game.NewGamePreset.Campaign;
                            mainMenuVM.m_ChargenUnit?.Dispose();
                            using (ContextData<UnitEntityData.ChargenUnit>.Request())
                            {
                                var unit = new UnitEntityData(mainCharacterId, true, Game.NewGamePreset.PlayerCharacter);
                                mainMenuVM.m_ChargenUnit = unit;
                                mainMenuVM.m_ChargenUnit.AttachToViewOnLoad(null);
                            }
                        }

                        Kingmaker.UnitLogic.Class.LevelUp.LevelUpConfig.Create(mainMenuVM.m_ChargenUnit, Kingmaker.UnitLogic.Class.LevelUp.LevelUpState.CharBuildMode.CharGen)
                            .SetEnterNewGameAction(OnCharacterCreated)
                            .OpenUI();

                        mainMenuVM.m_OpenCharGenCommand.Execute();
                    }
                }

                mainMenuVM.NewGameVM = new NewGameVM(PreviousStep, NextStep);
                mainMenuVM.m_OpenNewGameCommand.Execute();
                mainMenuVM.UpdateSoundState();
            });
        }

        public void SelectNewGameDifficulty(string difficulty)
        {
            _mainThreadAccessor.Post(() =>
            {
                var newGameVM = Game.Instance.RootUiContext?.MainMenuVM?.NewGameVM;
                if (newGameVM == null)
                {
                    _logger.LogWarning("Unable to select new game difficulty due to missing NewGameVM. Difficulty={Difficulty}", difficulty);
                    return;
                }

                if (!(newGameVM.DifficultyVM?.IsVisible.Value ?? false))
                {
                    _logger.LogWarning("Unable to select new game difficulty due to not visible DifficultyVM. Difficulty={Difficulty}", difficulty);
                    return;
                }

                var difficultySetting = newGameVM.DifficultyVM.m_SettingEntities.FirstOrDefault(s => s is SettingsEntityWithValueVM valueSetting && valueSetting.m_UISettingsEntity?.SettingsEntity == SettingsRoot.Difficulty.GameDifficulty);
                if (difficultySetting == null)
                {
                    _logger.LogError("Unable to select new game difficulty due to missing GameDifficulty setting entity. Difficulty={Difficulty}", difficulty);
                    return;
                }

                var difficultyEntity = (UISettingsEntityGameDifficulty)((SettingsEntityWithValueVM)difficultySetting).m_UISettingsEntity;
                if (!Enum.TryParse<GameDifficultyOption>(difficulty, true, out var gameDifficultyOption))
                {
                    _logger.LogError("Unable to select new game difficulty due to invalid difficulty value. Difficulty={Difficulty}", difficulty);
                    return;
                }

                difficultyEntity.SetTempValue(gameDifficultyOption);

                _logger.LogInformation("New Game difficulty has been selected. Difficulty={Difficulty}", difficultyEntity.GetTempValue());
            });
        }

        public string GetPetOwnerId(string unitId)
        {
            var unit = _gameStateLookupService.GetUnitEntity(unitId);
            if (unit == null)
            {
                return null;
            }

            if (unit.IsPet && unit.Master == null)
            {
                _logger.LogError("Pet has no master. UnitId={UnitId}", unitId);
                return null;
            }

            return unit.IsPet ? unit.Master.UniqueId : null;
        }

        public void ClickUnit(NetworkClick networkClick)
        {
            try
            {
                var clickUnitHandler = Game.Instance.DefaultPointerController.m_ClickHandlers.FirstOrDefault(c => c is ClickUnitHandler);
                if (clickUnitHandler == null)
                {
                    _logger.LogError("Unable to find ClickUnitHandler");
                    return;
                }

                ExecuteClickHandler(clickUnitHandler, networkClick);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiate click handler. HandlerType={HandlerType}", typeof(ClickUnitHandler));
                throw;
            }
        }

        public void ClickGroundInCombat(NetworkClick networkClick)
        {
            try
            {
                var clickGroundHandler = Game.Instance.DefaultPointerController.m_ClickHandlers.FirstOrDefault(c => c is ClickGroundHandler);
                if (clickGroundHandler == null)
                {
                    _logger.LogError("Unable to find ClickGroundHandler");
                    return;
                }

                ExecuteClickHandler(clickGroundHandler, networkClick);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiate click handler. HandlerType={HandlerType}", typeof(ClickGroundHandler));
                throw;
            }
        }

        public void ClickMapObject(NetworkClick networkClick)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    // each client generates a random map object ID, so the easiest way is to look for the nearest bag(assuming its location is relatively the same)
                    var mapObject = networkClick.IsLootBagMapObject ? _gameStateLookupService.GetNeareastLootBagMapObject(networkClick.WorldPosition) : _gameStateLookupService.GetMapObject(networkClick.MapObjectId);
                    if (mapObject == null)
                    {
                        _logger.LogWarning("Unable to click missing map object. MapObjectId={MapObjectId}", networkClick.MapObjectId);
                        return;
                    }

                    var selectedUnits = networkClick.SelectedUnits.Select(_gameStateLookupService.GetUnitEntity).ToList();

                    ClickMapObjectHandler.Interact(mapObject.View.gameObject, selectedUnits, forceOvertipInteractions: false, networkClick.MuteEvents);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to interact with map object. MapObjectId={MapObjectId}", networkClick.MapObjectId);
                    throw;
                }
            });
        }

        public void ToggleActivatableAbility(NetworkActivatableAbility networkActivatableAbility)
        {
            try
            {
                _mainThreadAccessor.Post(() =>
                {
                    var caster = _gameStateLookupService.GetUnitEntity(networkActivatableAbility.CasterId);
                    if (caster == null)
                    {
                        _logger.LogWarning("Caster of activatable ability doesn't exist. UnitId={UnitId}", networkActivatableAbility.CasterId);
                        return;
                    }

                    var ability = FindActivatableAbility(caster, networkActivatableAbility);
                    if (ability == null)
                    {
                        _logger.LogError("Unable to find activatable ability. UnitId={UnitId}, AbilityId={AbilityId}", caster.UniqueId, networkActivatableAbility.Id);
                        return;
                    }

                    var target = _gameStateLookupService.GetUnitEntity(networkActivatableAbility.TargetId);

                    ability.SetIsOn(networkActivatableAbility.IsActive, target);
                    _logger.LogInformation("Ability has been toggled. UnitId={UnitId}, AbilityId={AbilityId}, AbilityName={AbilityName}, IsActive={IsActive}, IsOn={IsOn}", caster.UniqueId, ability.UniqueId, ability.NameForAcronym, networkActivatableAbility.IsActive, ability.m_IsOn);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiate ToggleActivatableAbility.  CasterId={CasterId}, TargetId={TargetId}, AbilityId={AbilityId}", networkActivatableAbility.CasterId, networkActivatableAbility.TargetId, networkActivatableAbility.Id);
                throw;
            }
        }

        public void TransferInventoryItems(NetworkItemsTransfer networkItemsTransfer)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var lookupTargets = GetLootableEntitiesInventory(networkItemsTransfer.Source);

                    Dictionary<NetworkItem, List<ItemEntity>> matchedItems = null;
                    var sourceCollection = lookupTargets.FirstOrDefault(x => TryFindRequiredItemsInCollection(x, networkItemsTransfer.Items, out matchedItems));
                    if (sourceCollection == null)
                    {
                        _logger.LogError("Unable to find valid ItemsCollection source with all required items. Id={Id}, Position={Position}, Type={Type}", networkItemsTransfer.Source.Id, networkItemsTransfer.Source.Position, networkItemsTransfer.Source.Type);
                        _playerNotificationService.ShowWarningNotification(WellKnownKeys.GameNotifications.Looting.ItemsMismatch.Key);
                        return;
                    }

                    var destinationCollection = networkItemsTransfer.Destination == null ?
                        Game.Instance.Player.Inventory
                        : GetLootableEntitiesInventory(networkItemsTransfer.Destination).FirstOrDefault();

                    if (destinationCollection == null)
                    {
                        _logger.LogError("Unable to find valid ItemsCollection destination. Id={Id}, Position={Position}, Type={Type}", networkItemsTransfer.Destination.Id, networkItemsTransfer.Destination.Position, networkItemsTransfer.Destination.Type);
                        return;
                    }

                    foreach (var item in networkItemsTransfer.Items)
                    {
                        matchedItems.TryGetValue(item, out var containerItems);
                        MatchSameNumberOfItems(containerItems, item.Count, matchedItem =>
                        {
                            _logger.LogInformation("Transfering item. Name={Name}, Id={Id}, Count={Count}, Source={Source}, SourceIsStash={SourceIsStash}, Destination={Destination}, DestinationIsStash={DestinationIsStash}", matchedItem.Name, matchedItem.UniqueId, matchedItem.Count, sourceCollection.OwnerRef.Entity?.UniqueId, sourceCollection.IsSharedStash, destinationCollection.OwnerRef.Entity.UniqueId, destinationCollection.IsSharedStash);
                            sourceCollection.Transfer(matchedItem, matchedItem.Count, destinationCollection);
                        });
                    }

                    RefreshLootUI();
                    RefreshInventoryWindow();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to collect container loot");
                    throw;
                }
            });
        }

        public void SkinLootContainer(NetworkLootableEntity lootableEntity)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var unit = _gameStateLookupService.GetUnitEntity(lootableEntity.Id);
                    if (unit == null)
                    {
                        _logger.LogError("Unable to find unit to skin. UnitId={UnitId}, Position={Position}", lootableEntity.Id, lootableEntity.Position);
                        return;
                    }

                    var itemsToSkin = unit.Inventory.Where(i => i.NeedSkinningForCollect).ToList();
                    foreach (var item in itemsToSkin)
                    {
                        item.UseSkinning();
                    }

                    RefreshLootUI();
                    RefreshInventoryWindow();
                    _logger.LogInformation("Loot container has been skinned. ContainerId={ContainerId}, Position={Position}", lootableEntity.Id, lootableEntity.Position);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to use skinning on loot container");
                    throw;
                }
            });
        }

        public void DropItem(NetworkDropItem networkDropItem)
        {
            var entity = _gameStateLookupService.GetUnitEntity(networkDropItem.OwnerEntityId);
            if (entity == null)
            {
                _logger.LogError("Unable to find entity to drop item. EntityId={EntityId}", networkDropItem.OwnerEntityId);
                return;
            }

            var possibleItemsToDrop = entity.Inventory
                .Where(i => i.HoldingSlot == null && IsSameItem(i, networkDropItem.Item))
                .OrderBy(x => x.Count)
                .ToList();

            if (possibleItemsToDrop.Count == 0)
            {
                _logger.LogError("Unable to find item to drop. EntityId={EntityId}, ItemId={ItemId}, ItemName={ItemName}", networkDropItem.OwnerEntityId, networkDropItem.Item.UniqueId, networkDropItem.Item.Name);
                return;
            }

            var totalCount = possibleItemsToDrop.Sum(x => x.Count);
            if (totalCount < networkDropItem.Item.Count)
            {
                _logger.LogError("Not enough items to drop, possibly desynced somewhere else. EntityId={EntityId}, ItemId={ItemId}, TotalCount={TotalCount}, RequiredCount={RequiredCount}", networkDropItem.OwnerEntityId, networkDropItem.Item.UniqueId, totalCount, networkDropItem.Item.Count);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                MatchSameNumberOfItems(possibleItemsToDrop, networkDropItem.Item.Count,
                    (item) => DropItem(entity.Inventory, item, networkDropItem.OwnerEntityId));

                RefreshInventoryWindow();
            });
        }

        public void UseInventoryItem(NetworkUseInventoryItem useInventoryItem)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var itemToUse = useInventoryItem.SlotPosition != null ? GetItemSlot(useInventoryItem.Item.HoldingSlotOwnerId, useInventoryItem.SlotPosition)?.Item
                        : Game.Instance.Player.Inventory.Where(i => i.HoldingSlot == null && IsSameItem(i, useInventoryItem.Item)).FirstOrDefault();

                    if (itemToUse == null)
                    {
                        _logger.LogError("Unable to find item to use. ItemId={ItemId}, ItemName={ItemName}, HoldingSlowOwnerId={HoldingSlowOwnerId}, SlotType={SlotType}, SlotIndex={SlotIndex}", useInventoryItem.Item.UniqueId, useInventoryItem.Item.Name, useInventoryItem.Item.HoldingSlotOwnerId, useInventoryItem.SlotPosition?.Type, useInventoryItem.SlotPosition?.Index);
                        return;
                    }

                    var userEntity = _gameStateLookupService.GetUnitEntity(useInventoryItem.UserUnitId);
                    if (userEntity == null)
                    {
                        _logger.LogError("Unable to find user to use item. UserUnitId={UserUnitId}, ItemId={ItemId}, ItemName={ItemName}", useInventoryItem.UserUnitId, useInventoryItem.Item.UniqueId, useInventoryItem.Item.Name);
                        return;
                    }

                    using var context = _networkExecutionContext.Value = RemoteExecutionContext.CreateUseInventoryItem(itemToUse.UniqueId, userEntity.UniqueId);
                    if (useInventoryItem.SlotPosition != null)
                    {
                        context.Equipment = new EquipmentContext { Position = useInventoryItem.SlotPosition };
                    }

                    var target = CreateTargetWrapper(useInventoryItem.Target);
                    var isOk = itemToUse.TryUseFromInventory(userEntity, target);
                    if (!isOk)
                    {
                        _logger.LogWarning("Item usage has been failed. UserUnitId={UserUnitId}, TargetEntityId={TargetEntityId}, ItemId={ItemId}, ItemName={ItemName}", userEntity.UniqueId, target?.Unit?.UniqueId, itemToUse.UniqueId, itemToUse.Name);
                    }

                    RefreshInventoryWindow();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to use inventory item. UserUnitId={UserUnitId}, ItemId={ItemId}, ItemName={ItemName}", useInventoryItem.UserUnitId, useInventoryItem.Item.UniqueId, useInventoryItem.Item.Name);
                    throw;
                }
            });
        }

        public NetworkEquipmentSlotPosition GetEquipmentSlotPosition(ItemSlot slot)
        {
            if (slot == null)
            {
                return null;
            }

            var type = slot.GetType();
            var slotType = _equipmentDefinitions.GetSlotType(type);
            if (slotType == null)
            {
                _logger.LogWarning("Unable to get slot type. SlotType={slotType}, OwnerId={OwnerId}", type, slot.Owner.Unit.UniqueId);
                return null;
            }

            // let's just hope that order is the same everytime on everyclient
            var sameTypeItems = slot.Owner.Unit.Body.EquipmentSlots
                .Where(s => s.GetType() == slot.GetType())
                .ToList();

            var slotIndex = sameTypeItems.IndexOf(slot);

            return new NetworkEquipmentSlotPosition
            {
                Index = slotIndex,
                Type = slotType.Value,
            };
        }

        public void UpdateEquipmentSlot(NetworkEquipmentSlot networkEquipmentSlot)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = _gameStateLookupService.GetUnitEntity(networkEquipmentSlot.OwnerId);
                if (unit == null)
                {
                    _logger.LogError("Unable to update equipment slot for missing unit. UnitId={UnitId}", networkEquipmentSlot.OwnerId);
                    return;
                }

                var slotToUpdate = GetItemSlot(networkEquipmentSlot.OwnerId, networkEquipmentSlot.Position);
                if (slotToUpdate == null)
                {
                    _logger.LogError("Unable to find item slot to update equipment. UnitId={UnitId}, SlotType={SlotType}, SlotIndex={SlotIndex}", networkEquipmentSlot.OwnerId, networkEquipmentSlot.Position.Type, networkEquipmentSlot.Position.Index);
                    return;
                }

                using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(networkEquipmentSlot.Position);

                if (networkEquipmentSlot.Item == null)
                {
                    // Polymorphic items (Finnean) rely on this context to not delete item during slot change
                    using var swapContext = CreateSwapContext(unit, networkEquipmentSlot.SwapContext);

                    slotToUpdate.RemoveItem();
                    RefreshInventoryWindow();
                    _logger.LogInformation("Item has been unequipped. UnitId={UnitId}, SlotType={SlotType}, SlotIndex={SlotIndex}", networkEquipmentSlot.OwnerId, networkEquipmentSlot.Position.Type, networkEquipmentSlot.Position.Index);
                    return;
                }

                var item = unit.Inventory.Items.FirstOrDefault(i => string.Equals(i.UniqueId, networkEquipmentSlot.Item.UniqueId, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    // Stacking / splitting items generates a new item ID, which causes a mismatch on the clients,
                    // so we can find the 'same' unequipped item and equip it
                    var sameItem = unit.Inventory.Items.Where(i => i.HoldingSlot == null && IsSameItem(i, networkEquipmentSlot.Item))
                        .OrderBy(x => x.Count)
                        .FirstOrDefault();

                    if (sameItem == null)
                    {
                        _logger.LogError("Unable to update equipment slot with missing item. UnitId={UnitId}, SlotType={SlotType}, SlotIndex={SlotIndex}, ItemId={ItemId}", networkEquipmentSlot.OwnerId, networkEquipmentSlot.Position.Type, networkEquipmentSlot.Position.Index, networkEquipmentSlot.Item.UniqueId);
                        return;
                    }

                    // Split only works if count > 1, so it's safe to split everytime
                    item = sameItem.Split(1);
                }

                slotToUpdate.InsertItem(item);
                RefreshInventoryWindow();
                _logger.LogInformation("Item has been equipped. UnitId={UnitId}, SlotType={SlotType}, SlotIndex={SlotIndex}, ItemId={ItemId}", networkEquipmentSlot.OwnerId, networkEquipmentSlot.Position.Type, networkEquipmentSlot.Position.Index, networkEquipmentSlot.Item.UniqueId);
            });
        }

        public void SetActiveHandEquipmentSet(NetworkActiveHandEquipmentSet networkActiveHandEquipmentSet)
        {
            var unit = _gameStateLookupService.GetUnitEntity(networkActiveHandEquipmentSet.UnitId);
            if (unit == null)
            {
                _logger.LogError("Unable to set active hand equipment set for missing unit. UnitId={UnitId}", networkActiveHandEquipmentSet.UnitId);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                if (unit.Body.CurrentHandEquipmentSetIndex == networkActiveHandEquipmentSet.Index)
                {
                    return;
                }

                using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(networkActiveHandEquipmentSet);
                var previousIndex = unit.Body.CurrentHandEquipmentSetIndex;
                unit.Body.CurrentHandEquipmentSetIndex = networkActiveHandEquipmentSet.Index;
                RefreshInventoryWindow();
                if (_uiAccessor.VendorViewVM != null && string.Equals(_uiAccessor.VendorViewVM.DollVM?.Unit?.Value.Unit?.UniqueId, unit.UniqueId, StringComparison.OrdinalIgnoreCase))
                {
                    var set = _uiAccessor.VendorViewVM.DollVM.WeaponSets.FirstOrDefault(s => s.Index == unit.Body.CurrentHandEquipmentSetIndex);
                    if (set != null)
                    {
                        _uiAccessor.VendorViewVM.DollVM.CurrentSet.Value = set;
                    }
                }
                _logger.LogInformation("Changed active hand equipment slot. UnitId={UnitId}, PreviousIndex={PreviousIndex}, CurrentIndex={CurrentIndex}", networkActiveHandEquipmentSet.UnitId, previousIndex, unit.Body.CurrentHandEquipmentSetIndex);
            });
        }

        public EntityDataBase GetEntity(string id)
        {
            var entity = EntityService.Instance.GetEntity(id);
            return entity;
        }

        public bool IsSummoned(string unitId)
        {
            var unit = _gameStateLookupService.GetUnitEntity(unitId);
            return unit.IsSummoned();
        }

        public void ApplyPerceptionCheck(NetworkPerceptionCheck networkPerceptionCheck)
        {
            var mapObject = _gameStateLookupService.GetMapObject(networkPerceptionCheck.MapObject.Id);
            if (mapObject == null)
            {
                _logger.LogError("Unable to apply perception check due to missing map object. MapObjectId={MapObjectId}", networkPerceptionCheck.MapObject.Id);
                return;
            }

            var unit = _gameStateLookupService.GetUnitEntity(networkPerceptionCheck.UnitId);
            if (unit == null)
            {
                _logger.LogError("Unable to apply perception check due to missing unit. MapObjectId={MapObjectId}, UnitId={UnitId}", networkPerceptionCheck.MapObject.Id, networkPerceptionCheck.UnitId);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                // based on PartyPerceptionController.RollPerception
                try
                {
                    var perceptionCheckComponent = mapObject.View.PerceptionCheckComponent;
                    var rule = new RuleSkillCheck(unit, perceptionCheckComponent.StatType, perceptionCheckComponent.DC)
                    {
                        Reason = mapObject,
                        D20 = RuleRollD20.FromInt(unit, networkPerceptionCheck.Roll)
                    };
                    rule.m_Success = rule.IsSuccessRoll(rule.D20, rule.RequiresSuccessBonus ? rule.SuccessBonus : 0);
                    rule.m_Triggered = true;
                    _playerNotificationService.AddCombatText(rule);

                    var modifiableValue = unit.Stats.AllStats.FirstOrDefault(x => x.Type == perceptionCheckComponent.StatType);
                    if (modifiableValue == null)
                    {
                        _logger.LogError("Unable to do perception check due to missing character stat. UnitId={UnitId}, StatType={StatType}", unit.UniqueId, perceptionCheckComponent.StatType);
                        return;
                    }

                    mapObject.LastPerceptionRollRank[unit] = modifiableValue.BaseValue;
                    mapObject.IsPerceptionCheckPassed = rule.Success;

                    if (mapObject is SimpleTrapObjectData simpleTrap)
                    {
                        if (simpleTrap.LinkedTrap != null)
                        {
                            simpleTrap.LinkedTrap.IsPerceptionCheckPassed = rule.Success;
                        }

                        if (simpleTrap.Device != null)
                        {
                            simpleTrap.Device.IsPerceptionCheckPassed = rule.Success;
                        }
                    }

                    if (rule.Success)
                    {
                        EventBus.RaiseEvent<IPerceptionHandler>(x => x.OnEntityNoticed(mapObject, unit));
                    }

                    _logger.LogInformation("Perception check has been triggered. UnitId={UnitId}, MapObjectId={MapObjectId}, Roll={Roll}, IsSuccess={IsSuccess}", unit.UniqueId, mapObject.UniqueId, networkPerceptionCheck.Roll, rule.Success);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying perception check");
                    throw;
                }
            });
        }

        public void ApplyInspectionKnowledgeCheck(NetworkInspectionKnowledgeCheck networkInspectionKnowledgeCheck)
        {
            var targetUnit = _gameStateLookupService.GetUnitEntity(networkInspectionKnowledgeCheck.TargetUnitId);
            if (targetUnit == null)
            {
                _logger.LogError("Unable to apply inspection knowledge check due to missing target unit. TargetUnitId={TargetUnitId}", networkInspectionKnowledgeCheck.TargetUnitId);
                return;
            }

            var initiatorUnit = _gameStateLookupService.GetUnitEntity(networkInspectionKnowledgeCheck.InitiatorUnitId);
            if (initiatorUnit == null)
            {
                _logger.LogError("Unable to apply inspection knowledge check due to missing initiator unit. InitiatorUnitId={InitiatorUnitId}", networkInspectionKnowledgeCheck.InitiatorUnitId);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                try

                {
                    var blueprintForInspection = targetUnit.Descriptor.BlueprintForInspection;
                    InspectUnitsManager.UnitInfo info = Game.Instance.Player.InspectUnitsManager.GetInfo(blueprintForInspection);
                    if (info == null)
                    {
                        info = new InspectUnitsManager.UnitInfo(blueprintForInspection);
                        Game.Instance.Player.InspectUnitsManager.m_UnitInfos.Add(info);
                    }

                    if (info.IsAllPartsUnlocked)
                    {
                        return;
                    }

                    var rule = new RuleSkillCheck(initiatorUnit, networkInspectionKnowledgeCheck.StatType, networkInspectionKnowledgeCheck.DC)
                    {
                        IgnoreDifficultyBonusToDC = true,
                        D20 = RuleRollD20.FromInt(initiatorUnit, networkInspectionKnowledgeCheck.RollResult)
                    };
                    rule.m_Success = rule.IsSuccessRoll(rule.D20, rule.RequiresSuccessBonus ? rule.SuccessBonus : 0);

                    info.SetCheck(rule.RollResult);

                    _playerNotificationService.AddCombatText(rule);
                    _logger.LogInformation("Inspection knowledge check has been applied. InitiatorUnitId={InitiatorUnitId}, TargetUnitId={TargetUnitId}, StatType={StatType}, RollResult={RollResult}", networkInspectionKnowledgeCheck.InitiatorUnitId, networkInspectionKnowledgeCheck.TargetUnitId, networkInspectionKnowledgeCheck.StatType, networkInspectionKnowledgeCheck.RollResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while applying inspection check. InitiatorUnitId={InitiatorUnitId}, TargetUnitId={TargetUnitId}, StatType={StatType}, RollResult={RollResult}", networkInspectionKnowledgeCheck.InitiatorUnitId, networkInspectionKnowledgeCheck.TargetUnitId, networkInspectionKnowledgeCheck.StatType, networkInspectionKnowledgeCheck.RollResult);
                    throw;
                }
            });
        }

        public void ApplyStealthPerceptionCheck(NetworkStealthPerceptionCheck networkStealthPerceptionCheck)
        {
            _mainThreadAccessor.Post(() =>
            {
                var initiatorUnitId = _gameStateLookupService.GetUnitEntity(networkStealthPerceptionCheck.InitiatorId);
                if (initiatorUnitId == null)
                {
                    _logger.LogError("Unable to apply stealth perception check due to missing initiator unit. InitiatorId={InitiatorId}", networkStealthPerceptionCheck.InitiatorId);
                    return;
                }

                var stealhedUnitId = _gameStateLookupService.GetUnitEntity(networkStealthPerceptionCheck.StealthedUnitId);
                if (stealhedUnitId == null)
                {
                    _logger.LogError("Unable to apply stealth perception check due to missing stealther unit. StealthedUnitId={StealthedUnitId}", networkStealthPerceptionCheck.StealthedUnitId);
                    return;
                }

                initiatorUnitId.CachedPerceptionRoll = networkStealthPerceptionCheck.Roll;
                var perceptionCheck = new RuleCachedPerceptionCheck(initiatorUnitId, networkStealthPerceptionCheck.DC)
                {
                    Silent = true,
                    IsTargetInvisible = networkStealthPerceptionCheck.IsTargetInvisible,
                    IgnoreDifficultyBonusToDC = networkStealthPerceptionCheck.IgnoreDifficultyBonusToDC
                };
                perceptionCheck = Rulebook.Trigger(perceptionCheck);

                // using UnitStealthController.TickUnit as a reference
                EventBus.RaiseEvent<IUnitInStealthSpottedHandler>(x => x.HandleUnitInStealthSpotted(stealhedUnitId, perceptionCheck));
                if (stealhedUnitId.Stealth.AddSpottedBy(initiatorUnitId))
                {
                    EventBus.RaiseEvent<IUnitSpottedHandler>(x => x.HandleUnitSpotted(stealhedUnitId, initiatorUnitId));
                }

                if (UnitStealthController.SpotterBreaksStealth(stealhedUnitId, initiatorUnitId))
                {
                    stealhedUnitId.Descriptor.State.IsInStealth = false;
                    stealhedUnitId.Stealth.Clear();
                    if (stealhedUnitId.IsPlayerFaction)
                    {
                        stealhedUnitId.Stealth.WantEnterStealth = false;
                    }
                }

                _logger.LogInformation("Stealth perception check has been applied. InitiatorId={InitiatorId}, StealthedUnitId={StealthedUnitId}", initiatorUnitId, stealhedUnitId);
            });
        }

        public NetworkGameSettings GetGameSettings()
        {
            var settings = new NetworkGameSettings
            {
                TurnBased = new NetworkTurnBasedSettngs
                {
                    IsTurnBasedModeEnabled = SettingsRoot.Game.TurnBased.EnableTurnBasedMode.GetValue(),
                    AutoEndTurn = SettingsRoot.Game.TurnBased.AutoEndTurn.GetValue(),
                    AutoStopAfterFirstMoveAction = SettingsRoot.Game.TurnBased.AutoStopAfterFirstMoveAction.GetValue(),
                    TimeScaleInPlayerTurn = SettingsRoot.Game.TurnBased.TimeScaleInPlayerTurn.GetValue(),
                    TimeScaleInNonPlayerTurn = SettingsRoot.Game.TurnBased.TimeScaleInNonPlayerTurn.GetValue(),
                },
                Main = new NetworkGameMainSettings
                {
                    LootInCombat = SettingsRoot.Game.Main.LootInCombat.GetValue(),
                    QuickMovement = SettingsRoot.Game.Main.AcceleratedMove.GetValue(),
                    AutofillActionbarSlots = SettingsRoot.Game.Main.AutofillActionbarSlots.GetValue(),
                },
                Autopause = new NetworkAutopauseSettings
                {
                    ContinueMovementOnEngagement = SettingsRoot.Game.Autopause.ContinueMovementOnEngagement.GetValue(),
                    PauseOnAllyDown = SettingsRoot.Game.Autopause.PauseOnAllyDown.GetValue(),
                    PauseOnAreaLoaded = SettingsRoot.Game.Autopause.PauseOnAreaLoaded.GetValue(),
                    PauseOnAttackOfOpportunity = SettingsRoot.Game.Autopause.PauseOnAttackOfOpportunity.GetValue(),
                    PauseOnEndedBuffSummon = SettingsRoot.Game.Autopause.PauseOnEndedBuffSummon.GetValue(),
                    PauseOnEndOfPartyMembersRound = SettingsRoot.Game.Autopause.PauseOnEndOfPartyMembersRound.GetValue(),
                    PauseOnEndOfRound = SettingsRoot.Game.Autopause.PauseOnEndOfRound.GetValue(),
                    PauseOnEnemyDown = SettingsRoot.Game.Autopause.PauseOnEnemyDown.GetValue(),
                    PauseOnEnemySpotted = SettingsRoot.Game.Autopause.PauseOnEnemySpotted.GetValue(),
                    PauseOnEngagement = SettingsRoot.Game.Autopause.PauseOnEngagement.GetValue(),
                    PauseOnHiddenObjectDetected = SettingsRoot.Game.Autopause.PauseOnHiddenObjectDetected.GetValue(),
                    PauseOnLostFocus = SettingsRoot.Game.Autopause.PauseOnLostFocus.GetValue(),
                    PauseOnLowHealth = SettingsRoot.Game.Autopause.PauseOnLowHealth.GetValue(),
                    PauseOnMeleeEngagement = SettingsRoot.Game.Autopause.PauseOnMeleeEngagement.GetValue(),
                    PauseOnNewEnemyAppeared = SettingsRoot.Game.Autopause.PauseOnNewEnemyAppeared.GetValue(),
                    PauseOnPartyIsAttacked = SettingsRoot.Game.Autopause.PauseOnPartyIsAttacked.GetValue(),
                    PauseOnPartyMemberFinishedAbility = SettingsRoot.Game.Autopause.PauseOnPartyMemberFinishedAbility.GetValue(),
                    PauseOnPartyMemberRanOutOfConsumable = SettingsRoot.Game.Autopause.PauseOnPartyMemberRanOutOfConsumable.GetValue(),
                    PauseOnSpellcastFinished = SettingsRoot.Game.Autopause.PauseOnSpellcastFinished.GetValue(),
                    PauseOnSpellcastInterrupted = SettingsRoot.Game.Autopause.PauseOnSpellcastInterrupted.GetValue(),
                    PauseOnSpellcastStarted = SettingsRoot.Game.Autopause.PauseOnSpellcastStarted.GetValue(),
                    PauseOnTrapDetected = SettingsRoot.Game.Autopause.PauseOnTrapDetected.GetValue(),
                    PauseOnWeaponIsIneffective = SettingsRoot.Game.Autopause.PauseOnWeaponIsIneffective.GetValue(),
                    PauseWhenAllyUnconscious = SettingsRoot.Game.Autopause.PauseWhenAllyUnconscious.GetValue(),
                    PauseWhenEnemyUnconscious = SettingsRoot.Game.Autopause.PauseWhenEnemyUnconscious.GetValue(),
                    PauseWhenLastSleepingEnemyStays = SettingsRoot.Game.Autopause.PauseWhenLastSleepingEnemyStays.GetValue(),
                },

            };

            return settings;
        }

        public void ApplyGameSettings(NetworkGameSettings networkGameSettings)
        {
            if (networkGameSettings == null)
            {
                _logger.LogWarning("Game settings are null");
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                _logger.LogInformation("Applying settings");

                if (networkGameSettings.TurnBased != null)
                {
                    SettingsRoot.Game.TurnBased.EnableTurnBasedMode.SetValueAndConfirm(networkGameSettings.TurnBased.IsTurnBasedModeEnabled);
                    SettingsRoot.Game.TurnBased.AutoEndTurn.SetValueAndConfirm(networkGameSettings.TurnBased.AutoEndTurn);
                    SettingsRoot.Game.TurnBased.AutoStopAfterFirstMoveAction.SetValueAndConfirm(networkGameSettings.TurnBased.AutoStopAfterFirstMoveAction);
                    if (networkGameSettings.TurnBased.TimeScaleInPlayerTurn.HasValue)
                    {
                        SettingsRoot.Game.TurnBased.TimeScaleInPlayerTurn.SetValueAndConfirm(networkGameSettings.TurnBased.TimeScaleInPlayerTurn.Value);
                    }
                    if (networkGameSettings.TurnBased.TimeScaleInNonPlayerTurn.HasValue)
                    {
                        SettingsRoot.Game.TurnBased.TimeScaleInNonPlayerTurn.SetValueAndConfirm(networkGameSettings.TurnBased.TimeScaleInNonPlayerTurn.Value);
                    }
                }

                if (networkGameSettings.Main != null)
                {
                    SettingsRoot.Game.Main.LootInCombat.SetValueAndConfirm(networkGameSettings.Main.LootInCombat);
                    SettingsRoot.Game.Main.AcceleratedMove.SetValueAndConfirm(networkGameSettings.Main.QuickMovement);
                    if (networkGameSettings.Main.AutofillActionbarSlots.HasValue)
                    {
                        SettingsRoot.Game.Main.AutofillActionbarSlots.SetValueAndConfirm(networkGameSettings.Main.AutofillActionbarSlots.Value);
                    }
                }

                if (networkGameSettings.Autopause != null)
                {
                    SettingsRoot.Game.Autopause.ContinueMovementOnEngagement.SetValueAndConfirm(networkGameSettings.Autopause.ContinueMovementOnEngagement);
                    SettingsRoot.Game.Autopause.PauseOnAllyDown.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnAllyDown);
                    SettingsRoot.Game.Autopause.PauseOnAreaLoaded.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnAreaLoaded);
                    SettingsRoot.Game.Autopause.PauseOnAttackOfOpportunity.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnAttackOfOpportunity);
                    SettingsRoot.Game.Autopause.PauseOnEndedBuffSummon.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnEndedBuffSummon);
                    SettingsRoot.Game.Autopause.PauseOnEndOfPartyMembersRound.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnEndOfPartyMembersRound);
                    SettingsRoot.Game.Autopause.PauseOnEndOfRound.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnEndOfRound);
                    SettingsRoot.Game.Autopause.PauseOnEnemyDown.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnEnemyDown);
                    SettingsRoot.Game.Autopause.PauseOnEnemySpotted.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnEnemySpotted);
                    SettingsRoot.Game.Autopause.PauseOnEngagement.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnEngagement);
                    SettingsRoot.Game.Autopause.PauseOnHiddenObjectDetected.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnHiddenObjectDetected);
                    SettingsRoot.Game.Autopause.PauseOnLostFocus.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnLostFocus);
                    SettingsRoot.Game.Autopause.PauseOnLowHealth.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnLowHealth);
                    SettingsRoot.Game.Autopause.PauseOnMeleeEngagement.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnMeleeEngagement);
                    SettingsRoot.Game.Autopause.PauseOnNewEnemyAppeared.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnNewEnemyAppeared);
                    SettingsRoot.Game.Autopause.PauseOnPartyIsAttacked.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnPartyIsAttacked);
                    SettingsRoot.Game.Autopause.PauseOnPartyMemberFinishedAbility.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnPartyMemberFinishedAbility);
                    SettingsRoot.Game.Autopause.PauseOnPartyMemberRanOutOfConsumable.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnPartyMemberRanOutOfConsumable);
                    SettingsRoot.Game.Autopause.PauseOnSpellcastFinished.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnSpellcastFinished);
                    SettingsRoot.Game.Autopause.PauseOnSpellcastInterrupted.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnSpellcastInterrupted);
                    SettingsRoot.Game.Autopause.PauseOnSpellcastStarted.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnSpellcastStarted);
                    SettingsRoot.Game.Autopause.PauseOnTrapDetected.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnTrapDetected);
                    SettingsRoot.Game.Autopause.PauseOnWeaponIsIneffective.SetValueAndConfirm(networkGameSettings.Autopause.PauseOnWeaponIsIneffective);
                    SettingsRoot.Game.Autopause.PauseWhenAllyUnconscious.SetValueAndConfirm(networkGameSettings.Autopause.PauseWhenAllyUnconscious);
                    SettingsRoot.Game.Autopause.PauseWhenEnemyUnconscious.SetValueAndConfirm(networkGameSettings.Autopause.PauseWhenEnemyUnconscious);
                    SettingsRoot.Game.Autopause.PauseWhenLastSleepingEnemyStays.SetValueAndConfirm(networkGameSettings.Autopause.PauseWhenLastSleepingEnemyStays);
                }

                if (networkGameSettings.Tutorial != null)
                {
                    SettingsRoot.Game.Tutorial.ShowArmiesTutorial.SetValueAndConfirm(networkGameSettings.Tutorial.ShowArmiesTutorial);
                    SettingsRoot.Game.Tutorial.ShowBasicTutorial.SetValueAndConfirm(networkGameSettings.Tutorial.ShowBasicTutorial);
                    SettingsRoot.Game.Tutorial.ShowContextTutorial.SetValueAndConfirm(networkGameSettings.Tutorial.ShowContextTutorial);
                    SettingsRoot.Game.Tutorial.ShowControlsAdvancedTutorial.SetValueAndConfirm(networkGameSettings.Tutorial.ShowControlsAdvancedTutorial);
                    SettingsRoot.Game.Tutorial.ShowControlsBasicTutorial.SetValueAndConfirm(networkGameSettings.Tutorial.ShowControlsBasicTutorial);
                    SettingsRoot.Game.Tutorial.ShowCrusadeTutorial.SetValueAndConfirm(networkGameSettings.Tutorial.ShowCrusadeTutorial);
                    SettingsRoot.Game.Tutorial.ShowGameplayAdvancedTutorial.SetValueAndConfirm(networkGameSettings.Tutorial.ShowGameplayAdvancedTutorial);
                    SettingsRoot.Game.Tutorial.ShowGameplayBasicTutorial.SetValueAndConfirm(networkGameSettings.Tutorial.ShowGameplayBasicTutorial);
                    SettingsRoot.Game.Tutorial.ShowPathfinderRulesTutorial.SetValueAndConfirm(networkGameSettings.Tutorial.ShowPathfinderRulesTutorial);
                }

                if (networkGameSettings.Multiplayer != null)
                {
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.RemoteRollRetrievalTimeout.Key, networkGameSettings.Multiplayer.RemoteRollRetrievalTimeout.ToString());
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.NetworkAwaiterTimeout.Key, networkGameSettings.Multiplayer.NetworkAwaiterTimeout.ToString());
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.RestEncounterSyncTimeout.Key, networkGameSettings.Multiplayer.RestEncounterSyncTimeout.ToString());
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.CombatTurnDelayForAI.Key, networkGameSettings.Multiplayer.CombatTurnDelayForAI.ToString());
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.EnforcedCombatStartDelay.Key, networkGameSettings.Multiplayer.EnforcedCombatStartDelay);
                }
            });
        }

        public void SpawnCampPlace(NetworkVector3 position)
        {
            _mainThreadAccessor.Post(() =>
            {
                var campPosition = position.ToUnityVector3();
                _logger.LogInformation("Spawning camp place. Position={Position}", position);
                RestHelper.SpawnCampPlace(campPosition);
            });
        }

        public void SetCampingUseHealingSpells(bool isOn)
        {
            _mainThreadAccessor.Post(() =>
            {
                var restView = _uiAccessor.RestView;
                if (restView == null)
                {
                    return;
                }

                restView.m_HealingToggle.isOn = isOn;

                var campingState = Game.Instance.Player.Camping;
                campingState.UseSpells = isOn;
            });
        }

        public void SetCampingState(NetworkCampingState networkCampingState)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var restView = _uiAccessor.RestView;
                    if (restView != null)
                    {
                        restView.m_AutotuneToggle.isOn = networkCampingState.AutotuneIterationsStatus;
                        restView.ViewModel?.HandleIterationsCountCalculated(networkCampingState.IterationsCount);
                    }

                    var campingState = Game.Instance.Player.Camping;

                    UpdateCookingRecipe(campingState, networkCampingState.CookingBlueprintRecipeId);
                    UpdateAlchemistRecipe(campingState, networkCampingState.PotionBlueprintRecipeId);
                    UpdateScrollScribingRecipe(campingState, networkCampingState.ScrollBlueprintRecipeId);

                    campingState.RestIterationsCount = networkCampingState.IterationsCount;
                    campingState.m_AutoTuneRestIterations = networkCampingState.AutotuneIterationsStatus;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during updating camping state");
                    throw;
                }
            });
        }

        public void SetCampingRoles(List<NetworkCampingRole> networkCampingRoles)
        {
            _mainThreadAccessor.Post(() =>
            {
                var campingState = Game.Instance.Player.Camping;
                foreach (var role in networkCampingRoles)
                {
                    campingState.CurrentCampingRoles[role.RoleType].PrimaryUnit = _gameStateLookupService.GetUnitEntity(role.PrimaryUnitId);
                    campingState.CurrentCampingRoles[role.RoleType].SecondaryUnit = _gameStateLookupService.GetUnitEntity(role.SecondaryUnitId);
                }
            });
        }

        public void UpdateGroupChangerUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (_uiAccessor.GroupChangerView == null)
                    {
                        return;
                    }

                    _uiAccessor.GroupChangerView.m_AcceptButton.Interactable = isInteractable;
                    _uiAccessor.GroupChangerView.m_CloseButton.Interactable = isInteractable;
                    var acceptButtonText = _uiAccessor.GroupChangerView.m_AcceptButton.GetComponentInChildren<TextMeshProUGUI>();
                    _uiSyncCountersService.UpdateButtonTextCounter(acceptButtonText, readyPlayersCount, totalPlayersCount);
                    _logger.LogInformation("Group changer UI state has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to update group changer view state");
                    throw;
                }
            });
        }

        public void AcceptGroupChangerParty()
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (_uiAccessor.GroupChangerView == null)
                    {
                        return;
                    }

                    var viewModel = _uiAccessor.GroupChangerView.ViewModel;
                    viewModel.InternalGo();
                    viewModel.m_ActionGo?.Invoke();
                    _logger.LogInformation("Group changer has been accepted");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to accept group changer");
                    throw;
                }
            });
        }

        public void ClickGroupChangerUnit(string unitId)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (_uiAccessor.GroupChangerView == null)
                    {
                        return;
                    }

                    var view = _uiAccessor.GroupChangerView.m_CharacterViews.FirstOrDefault(v => string.Equals(v.UnitRef.UniqueId, unitId, StringComparison.OrdinalIgnoreCase));
                    if (view == null)
                    {
                        _logger.LogError("Unable to find character view in group manager ui. UnitId={UnitId}", unitId);
                        return;
                    }

                    var viewModel = view.ViewModel;
                    viewModel.Click.Execute(viewModel);
                    _logger.LogInformation("Executed click on group manager unit. UnitId={UnitId}", unitId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to execute click for group manager unit. UnitId={UnitId}", unitId);
                    throw;
                }
            });
        }

        public void CloseGroupChangerUI()
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (_uiAccessor.GroupChangerView == null)
                    {
                        return;
                    }

                    _logger.LogInformation("Closing group changer view");
                    var viewModel = _uiAccessor.GroupChangerView.ViewModel;
                    viewModel?.m_ActionClose?.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to close group changer");
                    throw;
                }
            });
        }

        public void UpdateSkipTimeUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (_uiAccessor.SkipTimeView?.ViewModel == null)
                    {
                        _logger.LogWarning("Unable to update skip time due to missing UI");
                        return;
                    }

                    _logger.LogInformation("Updating skip time ui state. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);

                    _uiAccessor.SkipTimeView.m_CloseButton.Interactable = isInteractable;
                    _uiAccessor.SkipTimeView.m_HoursSlider.interactable = isInteractable;
                    _uiAccessor.SkipTimeView.m_SkipTimeButton.Interactable = isInteractable;
                    var skipTimeButtonText = _uiAccessor.SkipTimeView.m_SkipTimeButton.GetComponentInChildren<TextMeshProUGUI>();
                    _uiSyncCountersService.UpdateButtonTextCounter(skipTimeButtonText, readyPlayersCount, totalPlayersCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to update skip time ui");
                    throw;
                }
            });
        }

        public void CloseSkipTimeUI()
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var view = _uiAccessor.SkipTimeView;
                    if (view?.ViewModel == null)
                    {
                        _logger.LogWarning("Skip time UI is already closed");
                        return;
                    }

                    view.ViewModel.Close();
                    _logger.LogInformation("Skip time UI has been closed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to close Skip Time UI");
                    throw;
                }
            });
        }

        public void OpenSkipTimeUI()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.SkipTimeView?.ViewModel != null)
                {
                    _logger.LogWarning("Skip time UI is already opened");
                    return;
                }

                _uiAccessor.CloseAllWindows();

                EventBus.RaiseEvent<ISkipTimeWindowUIHandler>(x => x.HandleOpenSkipTime());
            });
        }

        public void CloseRestWindow()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.RestView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Rest window is already closed");
                    return;
                }

                view.CloseRest();
                _logger.LogInformation("Rest window has been closed");
            });
        }

        public void UpdateSkipTimeHours(float hours)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var view = _uiAccessor.SkipTimeView;
                    if (view?.ViewModel == null)
                    {
                        _logger.LogWarning("Unable to update skip time hours due to missing UI");
                        return;
                    }

                    view.m_HoursSlider.value = hours;
                    _logger.LogInformation("Skip time hours has been updated. Hours={Hours}", hours);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to update skip time hours slider");
                    throw;
                }
            });
        }

        public void StartSkipTime()
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var view = _uiAccessor.SkipTimeView;
                    if (view?.ViewModel == null)
                    {
                        _logger.LogWarning("Unable to start skip time due to missing UI");
                        return;
                    }

                    view.m_SkipTimeButton.OnLeftClick.Invoke();
                    _logger.LogInformation("Skip time has been started");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to start skip time");
                    throw;
                }
            });
        }

        public void UpdateRestUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var view = _uiAccessor.RestView;
                    if (view?.ViewModel == null)
                    {
                        return;
                    }

                    view.m_StartRestButton.Interactable = isInteractable;
                    view.m_HealingToggle.interactable = isInteractable;
                    view.m_HealingToggle.GetComponent<ObservablePointerClickTrigger>().enabled = isInteractable;
                    view.m_AutotuneToggle.interactable = isInteractable;
                    view.m_AutotuneToggle.GetComponent<ObservablePointerClickTrigger>().enabled = isInteractable;
                    view.m_AutoGroupButton.Interactable = isInteractable;
                    // non-global map = anyone can cancel
                    // global-map = ui owner (host) only
                    // ShowingResults has no close button visible, but Esc press should be denied anyway
                    var isCloseButtonInteractable = CanCancelRest(isInteractable);
                    view.m_CloseButton.interactable = isCloseButtonInteractable;
                    view.m_CloseButton.GetComponent<ObservablePointerClickTrigger>().enabled = isCloseButtonInteractable;

                    view.m_DivineServiceRoles.m_Button.Interactable = isInteractable;
                    UpdateRestPortraits(view.m_DivineServiceRoles.m_FirstPortraitsView, isInteractable);

                    view.m_CamouflageRoles.m_Button.Interactable = isInteractable;
                    UpdateRestPortraits(view.m_CamouflageRoles.m_FirstPortraitsView, isInteractable);

                    view.m_GuardRestRoles.m_Button.Interactable = isInteractable;
                    UpdateRestPortraits(view.m_GuardRestRoles.m_FirstPortraitsView, isInteractable);
                    UpdateRestPortraits(view.m_GuardRestRoles.m_SecondPortraitsView, isInteractable);

                    view.m_AlchemyRoles.m_Button.Interactable = isInteractable;
                    view.m_AlchemyRoles.m_BrothIconButton.Interactable = isInteractable;
                    view.m_AlchemyRoles.m_PotionIconButton.Interactable = isInteractable;
                    UpdateRestPortraits(view.m_AlchemyRoles.m_FirstPortraitsView, isInteractable);

                    view.m_ScribesRoles.m_Button.Interactable = isInteractable;
                    view.m_ScribesRoles.m_ScrollsIconButton.Interactable = isInteractable;
                    UpdateRestPortraits(view.m_ScribesRoles.m_FirstPortraitsView, isInteractable);

                    _uiSyncCountersService.UpdateButtonTextCounter(view.m_StartRestButtonText, readyPlayersCount, totalPlayersCount);

                    _logger.LogInformation("Rest UI has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to update start rest button state");
                    throw;
                }
            });
        }

        public void InitiateRest()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.RestView?.ViewModel != null)
                {
                    _logger.LogWarning("Rest window is already opened");
                    return;
                }

                _uiAccessor.CloseAllWindows();

                var isOk = RestHelper.TryStartRest();
                _logger.LogInformation("Rest has been initiated. Result={Result}", isOk);
            });
        }

        public void StartRest()
        {
            _mainThreadAccessor.Post(() =>
            {
                _uiAccessor.RestView?.StartRest();
            });
        }

        public void SetRandomEncounterContext(NetworkRandomEncounterContext networkRandomEncounterContext)
        {
            _networkExecutionContext.Value = RemoteExecutionContext.Create(networkRandomEncounterContext);
        }

        public void TryInterruptRestBanter(NetworkRestBanter networkRestBanter)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var currentBanterPlayer = Game.Instance.RestController?.m_BanterPlayer;
                    if (currentBanterPlayer == null || currentBanterPlayer.m_NextEntryIndex == 0)
                    {
                        return;
                    }

                    var currentEntryIndex = currentBanterPlayer.m_NextEntryIndex - 1;
                    var currentBark = currentBanterPlayer.m_Entries[currentEntryIndex];
                    if (!string.Equals(currentBark.Text.Key, networkRestBanter.Key, StringComparison.OrdinalIgnoreCase) || !string.Equals(currentBark.Speaker.UniqueId, networkRestBanter.SpeakerUnitId, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("RestController is playing different banter. CurrentBanterKey={CurrentBanterKey}, CurrentSpeakerUnitId={CurrentSpeakerUnitId}, NetworkBanterKey={NetworkBanterKey}, NetworkSpeakerUnitId={NetworkSpeakerUnitId}",
                                currentBark.Text.Key, currentBark.Speaker.UniqueId, networkRestBanter.Key, networkRestBanter.SpeakerUnitId);
                        return;
                    }

                    currentBanterPlayer.InterruptBark();
                    _logger.LogInformation("Rest bark has been interrupted. NetworkBanterKey={NetworkBanterKey}, NetworkSpeakerUnitId={NetworkSpeakerUnitId}", networkRestBanter.Key, networkRestBanter.SpeakerUnitId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to interrupd bark. NetworkBanterKey={NetworkBanterKey}, NetworkSpeakerUnitId={NetworkSpeakerUnitId}", networkRestBanter.Key, networkRestBanter.SpeakerUnitId);
                    throw;
                }
            });
        }

        public void TransferVendorItem(NetworkVendorItemTransfer networkVendorItemTransfer)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (networkVendorItemTransfer.ItemActionTarget == VendorItemActionTarget.Sell && networkVendorItemTransfer.ItemAction == VendorItemAction.Add)
                    {
                        // add for sell is different due to unsynced state of the player's inventory
                        AddItemToVendorSellCollection(networkVendorItemTransfer);
                        RefreshVendorScreen();
                        return;
                    }

                    var (item, action) = GetDataForVendorTransferAction(networkVendorItemTransfer.Item, networkVendorItemTransfer.Count, networkVendorItemTransfer.ItemActionTarget, networkVendorItemTransfer.ItemAction);
                    if (item == null)
                    {
                        _logger.LogError("Unable to find item for make vendor transfer action. ItemId={ItemId}, ActionTarget={ActionTarget}, ActionType={ActionType}", networkVendorItemTransfer.Item.UniqueId, networkVendorItemTransfer.ItemActionTarget, networkVendorItemTransfer.ItemAction);
                        return;
                    }

                    if (action == null)
                    {
                        _logger.LogError("Unable to find to determine correct action to make vendor transfer. ItemId={ItemId}, ActionTarget={ActionTarget}, ActionType={ActionType}", networkVendorItemTransfer.Item.UniqueId, networkVendorItemTransfer.ItemActionTarget, networkVendorItemTransfer.ItemAction);
                        return;
                    }

                    using var context = _networkExecutionContext.Value = RemoteExecutionContext.CreateVendorItemTransfer(item.UniqueId);
                    var transferredItem = action(item);
                    RefreshVendorScreen();
                    _logger.LogInformation("Vendor item has been transferred. ItemId={ItemId}, Count={Count}, ActionTarget={ActionTarget}, ActionType={ActionType}", transferredItem.UniqueId, transferredItem.Count, networkVendorItemTransfer.ItemActionTarget, networkVendorItemTransfer.ItemAction);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while transfering vendor items");
                    throw;
                }
            });
        }

        public void CloseVendorWindow()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.VendorViewVM == null)
                {
                    _logger.LogWarning("Unable to close vendor screen due to missing VendorViewVM");
                    return;
                }

                _uiAccessor.VendorViewVM.m_CloseAction?.Invoke();
                SetPause(false);
            });
        }

        public void MakeVendorDeal()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.VendorViewVM == null)
                {
                    _logger.LogWarning("Unable to make vendor deal due to missing VendorViewVM");
                    return;
                }

                _uiAccessor.VendorViewVM.Deal();
            });
        }

        public void ForgetSpell(string unitId, NetworkSpellSlot networkSpellSlot, NetworkAbility networkAbility)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = _gameStateLookupService.GetUnitEntity(unitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to find unit to forget spell. UnitId={UnitId}", unitId);
                    return;
                }

                var spellbook = _gameStateLookupService.GetSpellbook(unit, networkAbility.SpellbookId);
                if (spellbook == null)
                {
                    _logger.LogError("Unable to find spellbook to forget spell. UnitId={UnitId}, SpellbookId={SpellbookId}", unitId, networkAbility.SpellbookId);
                    return;
                }

                var spellSlot = _gameStateLookupService.GetSpellSlot(spellbook, networkSpellSlot, networkAbility.SpellLevel);
                if (spellSlot == null)
                {
                    _logger.LogError("Unable to find spellslot to forget. UnitId={UnitId}, SpellbookId={SpellbookId}, SpellSlotIndex={SpellSlotIndex}, SpellSlotType={SpellSlotType}", unitId, networkAbility.SpellbookId, networkSpellSlot?.Index, networkSpellSlot?.Type);
                    return;
                }

                _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.SpellBook.ForgottenSpell.Key, CombatTextSeverity.Common, new AbilityTooltipLog(spellSlot.SpellShell), spellSlot.SpellShell.Name, new UnitEntityLog(unit.UniqueId));
                spellbook.ForgetMemorized(spellSlot);
                RefreshSpellbookUI();
            });
        }

        public void MemorizeSpell(string unitId, NetworkSpellSlot networkSpellSlot, NetworkAbility networkAbility)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = _gameStateLookupService.GetUnitEntity(unitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to find unit to memorize spell. UnitId={UnitId}", unitId);
                    return;
                }

                var spellbook = _gameStateLookupService.GetSpellbook(unit, networkAbility.SpellbookId);
                if (spellbook == null)
                {
                    _logger.LogError("Unable to find spellbook to memorize spell. UnitId={UnitId}, SpellbookId={SpellbookId}", unitId, networkAbility.SpellbookId);
                    return;
                }

                var spellSlot = _gameStateLookupService.GetSpellSlot(spellbook, networkSpellSlot, networkAbility.SpellLevel);
                var spell = _gameStateLookupService.GetKnownSpell(spellbook, networkAbility);
                spellbook.Memorize(spell, spellSlot);
                _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.SpellBook.MemorizedSpell.Key, CombatTextSeverity.Common, new AbilityTooltipLog(spell), spell.Name, new UnitEntityLog(unit.UniqueId));
                RefreshSpellbookUI();
            });
        }

        public void RemoveCustomSpell(string unitId, NetworkAbility ability)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = _gameStateLookupService.GetUnitEntity(unitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to find unit to remove custom spell. UnitId={UnitId}", unitId);
                    return;
                }

                var spellbook = _gameStateLookupService.GetSpellbook(unit, ability.SpellbookId);
                if (spellbook == null)
                {
                    _logger.LogError("Unable to find spellbook to remove custom spell. UnitId={UnitId}, SpellbookId={SpellbookId}", unit.UniqueId, ability.SpellbookId);
                    return;
                }

                var spell = _gameStateLookupService.GetCustomSpell(spellbook, ability);
                if (spell == null)
                {
                    _logger.LogError("Unable to find remove missing custom spell. UnitId={UnitId}, SpellId={SpellId}, SpellName={SpellName}", unit.UniqueId, ability.Id, ability.Name);
                    return;
                }

                spellbook.RemoveCustomSpell(spell);
                _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.SpellBook.RemovedCustomSpell.Key, CombatTextSeverity.Common, new AbilityTooltipLog(spell), spell.Name, new UnitEntityLog(unit.UniqueId));
                _logger.LogInformation("Custom spell has been removed. UnitId={UnitId}, SpellName={SpellName}", unit.UniqueId, spell.NameForAcronym);
                RefreshSpellbookUI();
            });
        }

        public void CreateMetamagicSpell(NetworkMetamagicSpell metamagicSpell)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var unit = _gameStateLookupService.GetUnitEntity(metamagicSpell.UnitId);
                    if (unit == null)
                    {
                        _logger.LogError("Unable to find unit to create metamagic spell. UnitId={UnitId}", metamagicSpell.UnitId);
                        return;
                    }

                    var spellbook = _gameStateLookupService.GetSpellbook(unit, metamagicSpell.Ability.SpellbookId);
                    if (spellbook == null)
                    {
                        _logger.LogError("Unable to find spellbook to create metamagic spell. UnitId={UnitId}, SpellbookId={SpellbookId}", unit.UniqueId, metamagicSpell.Ability.SpellbookId);
                        return;
                    }

                    var spell = _gameStateLookupService.GetKnownSpell(spellbook, metamagicSpell.Ability);
                    if (spell == null)
                    {
                        _logger.LogError("Unable to find spell to create metamagic spell. UnitId={UnitId}, SpellId={SpellId}, SpellBlueprintId={SpellBlueprintId}, SpellName={SpellName}, SpellbookId={SpellbookId}", unit.UniqueId, metamagicSpell.Ability.Id, metamagicSpell.Ability.BlueprintId, metamagicSpell.Ability.Name, metamagicSpell.Ability.SpellbookId);
                        return;
                    }

                    using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(metamagicSpell);

                    var metamagicBuilder = new MetamagicBuilder(spellbook, spell);
                    foreach (var metamagicFeature in metamagicBuilder.KnownMetamagicFeatures)
                    {
                        if (metamagicSpell.MetamagicFeatures.Contains((int)metamagicFeature.GetComponent<AddMetamagicFeat>().Metamagic))
                        {
                            metamagicBuilder.AddMetamagic(metamagicFeature);
                        }
                    }

                    metamagicBuilder.SetHeightenLevel(metamagicSpell.HeightenLevel);
                    metamagicBuilder.Apply();

                    var metaSpell = metamagicBuilder.ResultAbilityData;
                    if (metamagicSpell.BorderNumber.HasValue)
                    {
                        metaSpell.DecorationBorderNumber = metamagicSpell.BorderNumber.Value;
                    }

                    if (metamagicSpell.DecorationColorNumber.HasValue)
                    {
                        metaSpell.DecorationColorNumber = metamagicSpell.DecorationColorNumber.Value;
                    }

                    var duplicate = GetDuplicateCustomSpell(spellbook, metaSpell);
                    if (duplicate != null)
                    {
                        var duplicateSpellMetamagicFlags = duplicate.MetamagicData.MetamagicMask.GetAllFlags();
                        _logger.LogInformation("Duplicate metamagic spell has been removed. UnitId={UnitId}, SpellName={SpellName}, Metamagic={Metamagic}", unit.UniqueId, duplicate.NameForAcronym, duplicateSpellMetamagicFlags);
                        _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.SpellBook.RemovedDuplicateMetamagicSpell.Key, CombatTextSeverity.Common, new AbilityTooltipLog(duplicate), duplicate.Name, new UnitEntityLog(unit.UniqueId));
                        spellbook.RemoveCustomSpell(duplicate);
                    }

                    spellbook.AddCustomSpell(metaSpell);
                    if (_uiAccessor.SpellbookPCView?.ViewModel != null)
                    {
                        _uiAccessor.SpellbookPCView.ViewModel.OnMetamagicComplete();
                    }

                    var metamagicFlags = metaSpell.MetamagicData.MetamagicMask.GetAllFlags();
                    _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.SpellBook.NewMetamagicSpell.Key, CombatTextSeverity.Common, new AbilityTooltipLog(metaSpell), metaSpell.Name, new UnitEntityLog(unit.UniqueId), string.Join(", ", metamagicFlags));
                    _logger.LogInformation("Metamagic spell has been created. UnitId={UnitId}, SpellName={SpellName}, MetamagicFeatures={MetamagicFeatures}", unit.UniqueId, metamagicSpell.Ability.Name, metamagicFlags);
                    RefreshSpellbookUI();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to create metamagic spell");
                    throw;
                }
            });
        }

        public void SelectNewGameSequencePhase(NetworkNewGameSequencePhase newGameSequencePhase)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (_uiAccessor.NewGamePCView?.ViewModel == null)
                    {
                        _logger.LogWarning("Unable to select new game sequence phase due to missing NewGamePCView. PhaseType={PhaseType}", newGameSequencePhase.Type);
                        return;
                    }

                    switch (newGameSequencePhase.Type)
                    {
                        case NetworkNewGameSequencePhaseType.Story:
                            _uiAccessor.NewGamePCView.ViewModel.MenuSelectionGroup.SelectedEntity.Value = _uiAccessor.NewGamePCView.ViewModel.m_MenuEntitiesList.FirstOrDefault(m => m.NewGamePhaseVM == _uiAccessor.NewGamePCView.ViewModel.StoryVM);
                            break;
                        case NetworkNewGameSequencePhaseType.Difficulty:
                            _uiAccessor.NewGamePCView.ViewModel.MenuSelectionGroup.SelectedEntity.Value = _uiAccessor.NewGamePCView.ViewModel.m_MenuEntitiesList.FirstOrDefault(m => m.NewGamePhaseVM == _uiAccessor.NewGamePCView.ViewModel.DifficultyVM);
                            break;
                        case NetworkNewGameSequencePhaseType.SaveInjector:
                            _uiAccessor.NewGamePCView.ViewModel.MenuSelectionGroup.SelectedEntity.Value = _uiAccessor.NewGamePCView.ViewModel.m_MenuEntitiesList.FirstOrDefault(m => m.NewGamePhaseVM == _uiAccessor.NewGamePCView.ViewModel.SaveInjectorVM);
                            break;
                    }

                    _logger.LogInformation("New game sequence phase has been selected. PhaseType={PhaseType}", newGameSequencePhase.Type);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to select new game sequence phase. PhaseType={PhaseType}", newGameSequencePhase.Type);
                    throw;
                }
            });
        }

        public void TerminateNewGameSequence()
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (_uiAccessor.NewGamePCView?.ViewModel == null)
                    {
                        _logger.LogWarning("Unable to terminate new game sequence due to missing NewGamePCView");
                        return;
                    }

                    _uiAccessor.NewGamePCView.ViewModel.OnClose();
                    _logger.LogInformation("New game sequence has been terminated");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to terminate new game sequence");
                    throw;
                }
            });
        }

        public void StartNewGameSequenceLeveling()
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (_uiAccessor.NewGamePCView?.ViewModel == null)
                    {
                        _logger.LogWarning("Unable to start new game sequence leveling due to missing NewGamePCView");
                        return;
                    }

                    _uiAccessor.NewGamePCView.ViewModel.MenuSelectionGroup.SelectedEntity.Value.OnNext();
                    _logger.LogInformation("New game sequence leveling has been selected");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to start new game sequence leveling");
                    throw;
                }
            });
        }

        public void UpdateNewGameSequencePhaseControls(bool isEnabled, NetworkNewGameSequencePhaseType newGameSequencePhaseType)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (_uiAccessor.NewGamePCView?.ViewModel == null)
                    {
                        _logger.LogWarning("Unable to update new game sequence phase controls due to missing NewGamePCView. IsEnabled={IsEnabled}, PhaseType={PhaseType}", isEnabled, newGameSequencePhaseType);
                        return;
                    }

                    _uiAccessor.NewGamePCView.m_BackButton.Interactable = isEnabled;
                    _uiAccessor.NewGamePCView.m_NextButton.Interactable = isEnabled;
                    _uiAccessor.NewGamePCView.m_CloseButton.Interactable = isEnabled;
                    foreach (var menu in _uiAccessor.NewGamePCView.m_MenuSelectorView.m_MenuEntities)
                    {
                        menu.m_Button.Interactable = isEnabled;
                    }

                    switch (newGameSequencePhaseType)
                    {
                        case NetworkNewGameSequencePhaseType.Story:
                            foreach (var widgetEntry in _uiAccessor.NewGamePCView.m_StoryPCView.m_SelectorPCView.m_WidgetList.Entries)
                            {
                                if (widgetEntry is NewGamePhaseStoryScenarioEntityPCView story)
                                {
                                    story.m_Button.Interactable = isEnabled && string.Equals(story.ViewModel.m_StoryCampaign.name, "MainCampaign", StringComparison.OrdinalIgnoreCase);
                                }
                            }
                            break;
                        case NetworkNewGameSequencePhaseType.Difficulty:
                            foreach (var entry in _uiAccessor.NewGamePCView.m_DifficultyPCView.m_VirtualList.Elements)
                            {
                                switch (entry.View)
                                {
                                    case SettingsEntityDropdownGameDifficultyPCView difficultyPCView:
                                        foreach (var difficultyItem in difficultyPCView.m_ItemViews)
                                        {
                                            difficultyItem.m_Button.Interactable = isEnabled;
                                        }
                                        break;
                                }
                            }
                            break;
                    }

                    _logger.LogInformation("New game sequence phase controls have been updated. IsEnabled={IsEnabled}, PhaseType={PhaseType}", isEnabled, newGameSequencePhaseType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to update new game sequence phase controls. IsEnabled={IsEnabled}, PhaseType={PhaseType}", isEnabled, newGameSequencePhaseType);
                    throw;
                }
            });
        }

        public void MoveActionBarSlots(NetworkActionBarSlot sourceActionBarSlot, NetworkActionBarSlot targetActionBarSlot)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var unit = _gameStateLookupService.GetUnitEntity(sourceActionBarSlot.UnitId);
                    if (unit == null)
                    {
                        _logger.LogError("Unable to move action bar slot for missing unit. UnitId={UnitId}", sourceActionBarSlot.UnitId);
                        return;
                    }

                    if (targetActionBarSlot.Index == -1)
                    {
                        return;
                    }

                    if (sourceActionBarSlot.Index == -1)
                    {
                        // adding from spells/abilities/items panel
                        var slot = LoadActionBarSlot(unit, sourceActionBarSlot);
                        if (slot == null)
                        {
                            _logger.LogError("Unable to load action bar slot from existing spells/abilities/items. UnitId={UnitId}, SourceSlotIndex={SourceSlotIndex}", sourceActionBarSlot.Index);
                            return;
                        }

                        unit.UISettings.SetSlot(slot, targetActionBarSlot.Index);
                        return;
                    }


                    var sourceSlot = unit.UISettings.GetSlot(sourceActionBarSlot.Index, unit);
                    // source can't be empty unless ActionBar was never initialized, e.g. right at the game start if you never had a control of character
                    if (sourceSlot is MechanicActionBarSlotEmpty)
                    {
                        unit.UISettings.TryToInitialize();
                        sourceSlot = unit.UISettings.GetSlot(sourceActionBarSlot.Index, unit);
                    }

                    var targetSlot = unit.UISettings.GetSlot(targetActionBarSlot.Index, unit);
                    unit.UISettings.SetSlot(sourceSlot, targetActionBarSlot.Index);
                    unit.UISettings.SetSlot(targetSlot, sourceActionBarSlot.Index);

                    var updatedSourceSlot = unit.UISettings.GetSlot(sourceActionBarSlot.Index, unit);
                    var updatedTargetSlot = unit.UISettings.GetSlot(targetActionBarSlot.Index, unit);

                    _logger.LogInformation("Action bar slots have been updated for unit. UnitId={UnitId}, SourceSlotType={SourceSlotType}, TargetSlotType={TargetSlotType}", unit.UniqueId, updatedSourceSlot.GetType().Name, updatedTargetSlot.GetType().Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to move action bar slots");
                    throw;
                }
            });
        }

        public void ClearActionBarSlot(NetworkActionBarSlot actionBarSlot)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = _gameStateLookupService.GetUnitEntity(actionBarSlot.UnitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to clear action bar slot for missing unit. UnitId={UnitId}", actionBarSlot.UnitId);
                    return;
                }

                if (actionBarSlot.Index == -1)
                {
                    return;
                }

                var emptySlot = new MechanicActionBarSlotEmpty();
                unit.UISettings.SetSlot(emptySlot, actionBarSlot.Index);
            });
        }

        public void LockpickMapObject(NetworkLockpickInteraction lockpickInteraction)
        {
            _mainThreadAccessor.Post(() =>
            {
                var mapObject = _gameStateLookupService.GetMapObject(lockpickInteraction.MapObject.Id);
                List<UnitEntityData> units = [.. lockpickInteraction.Units.Select(_gameStateLookupService.GetUnitEntity)];

                using var lockpickVM = new LockpickVM(mapObject.View, null);
                using var context = _networkExecutionContext.Value = new RemoteExecutionContext
                {
                    SelectedUnits = units,
                    Lockpick = new MapObjectLockpickContext { MapObjectId = lockpickInteraction.MapObject.Id }
                };
                lockpickVM.OnInteraction(lockpickInteraction.LockpickType);
            });
        }

        public void ChangeUnitStealth(string unitId, bool isEnabled, bool isForced)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = _gameStateLookupService.GetUnitEntity(unitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to change stealth due to missing unit. UnitId={UnitId}", unitId);
                    return;
                }

                unit.Stealth.WantEnterStealth = isEnabled;
                unit.Stealth.ForceEnterStealth = isForced;
                Game.Instance.StealthController.TickUnit(unit);

                _logger.LogInformation("Unit stealth has been changed. UnitId={UnitId}, IsEnabled={IsEnabled}, IsForced={IsForced}", unitId, isEnabled, isForced);
            });
        }

        public string GetUnitCharacterName(string unitId)
        {
            var unit = _gameStateLookupService.GetUnitEntity(unitId);
            return unit?.CharacterName;
        }

        public void UpdateZoneLootUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.LootPCView?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update closed zone loot ui");
                    return;
                }

                _uiAccessor.LootPCView.m_RemoveLootToggle.Interactable = isInteractable;
                _uiAccessor.LootPCView.m_Button.Interactable = isInteractable; // Leave button
                _uiAccessor.LootCollector.m_ButtonCollectAll.Interactable = isInteractable;

                _uiSyncCountersService.UpdateButtonTextCounter(_uiAccessor.LootCollector.m_ButtonCollectAllLabel, readyPlayersCount, totalPlayersCount);
                _uiSyncCountersService.UpdateButtonTextCounter(_uiAccessor.LootPCView.m_ButtonText, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("ZoneLoot UI has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateCharacterSelectionUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var window = UnityEngine.Object.FindObjectOfType<CharSelectWindow>();
                if (window == null)
                {
                    _logger.LogWarning("Character selection window is missing");
                    return;
                }

                foreach (var item in window.m_SelectorItems)
                {
                    item.Toggle.interactable = isInteractable;
                }

                window.m_OkButton.Interactable = window.CurrentCharacter != null && isInteractable;
                var closeButton = window.transform.Find("Window/CloseButton").GetComponent<OwlcatButton>();
                closeButton.Interactable = isInteractable;

                var okButtonText = window.m_OkButton.GetComponentInChildren<TextMeshProUGUI>();
                _uiSyncCountersService.UpdateButtonTextCounter(okButtonText, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Character selection UI has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void CloseCharacterSelectionWindow()
        {
            _mainThreadAccessor.Post(() =>
            {
                var window = UnityEngine.Object.FindObjectOfType<CharSelectWindow>();
                if (window == null)
                {
                    _logger.LogWarning("Character selection window is missing");
                    return;
                }

                window.CloseWindow();
                _logger.LogInformation("Character selection UI has been closed");
            });
        }

        public void AcceptCharacterSelectionWindow()
        {
            _mainThreadAccessor.Post(() =>
            {
                var window = UnityEngine.Object.FindObjectOfType<CharSelectWindow>();
                if (window == null)
                {
                    _logger.LogWarning("Character selection window is missing");
                    return;
                }

                window.OnButtonOk();
                _logger.LogInformation("Character selection UI has been accepted");
            });
        }

        public void ToggleCharacterSelectionWindow(string unitId)
        {
            _mainThreadAccessor.Post(() =>
            {
                var window = UnityEngine.Object.FindObjectOfType<CharSelectWindow>();
                if (window == null)
                {
                    _logger.LogWarning("Character selection window is missing");
                    return;
                }

                for (int i = 0; i < window.m_SelectCharacters.Count; i++)
                {
                    var character = window.m_SelectCharacters[i];
                    var heroPortrait = window.m_SelectorItems[i];
                    heroPortrait.Toggle.SetIsOnWithoutNotify(string.Equals(character.UniqueId, unitId, StringComparison.OrdinalIgnoreCase));
                }

                _logger.LogInformation("Character selection UI has been toggled. CurrentCharacterId={CurrentCharacterId}", window.CurrentCharacter?.UniqueId);
            });
        }

        public void UpdateZoneLootRemoveToggle(bool removeLoot)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.LootPCView?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update closed zone loot ui");
                    return;
                }

                _uiAccessor.LootPCView.ViewModel.RemoveUncollectedLoot.Value = removeLoot;

                _logger.LogInformation("ZoneLoot Remove Uncollected loot toggle has been updated. RemoveLoot={RemoveLoot}", removeLoot);
            });
        }

        public void CompleteZoneLoot()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.LootPCView?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update closed zone loot ui");
                    return;
                }

                _uiAccessor.LootPCView.ViewModel.CollectAll();
                _logger.LogError("ZoneLoot has been completed");
            });
        }

        public void LeaveZoneLoot()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.LootPCView?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update closed zone loot ui");
                    return;
                }

                _uiAccessor.LootPCView.ViewModel.LeaveZone();
                _logger.LogError("ZoneLoot has been left");
            });
        }

        public bool IsUnitBusy(string unitId)
        {
            var unit = _gameStateLookupService.GetUnitEntity(unitId);
            return (unit?.Commands.IsRunning() ?? false) || (unit?.AreHandsBusyWithAnimation ?? false);
        }

        public void SetUnitAutoUseAbility(NetworkAutoUseAbility autoUseAbility)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = _gameStateLookupService.GetUnitEntity(autoUseAbility.UnitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to change autouse ability due to missing unit. UnitId={UnitId}", autoUseAbility.UnitId);
                    return;
                }

                var ability = _gameStateLookupService.FindAbility(unit, autoUseAbility.Ability);
                unit.Brain.AutoUseAbility = ability;
                _logger.LogInformation("Unit AutoUseAbility has been changed. UnitId={UnitId}, AbilityId={AbilityId}, AbilityName={AbilityName}", autoUseAbility.UnitId, ability?.UniqueId, ability?.NameForAcronym);
            });
        }

        public bool IsDeadOrMissing(string unitId)
        {
            var unit = _gameStateLookupService.GetUnitEntity(unitId);

            return unit == null || unit.Descriptor.State.IsFinallyDead;
        }

        public void UpdateTransitionMapUIState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.TransitionPCView?.ViewModel != null)
                {
                    UpdateCapitalCityTransitionMap(_uiAccessor.TransitionPCView, isInteractable, readyPlayersCount, totalPlayersCount);
                    return;
                }
                else if (_uiAccessor.MapIslandsPCView?.ViewModel != null)
                {
                    UpdateIslandsTransitionMap(_uiAccessor.MapIslandsPCView, isInteractable, readyPlayersCount, totalPlayersCount);
                    return;
                }

                _logger.LogError("Unable to update transition due to missing maps");
            });
        }

        public void ChooseIslandMapEntry(NetworkIslandMapTransition islandMapTransition)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.MapIslandsPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to choose entry due to missing TransitionPCView");
                    return;
                }

                var islandPosition = islandMapTransition.Position.ToUnityVector2Int();
                var island = view.m_IslandsItemViews.FirstOrDefault(x => IsSameIsland(x.ViewModel, islandPosition, islandMapTransition.BlueprintId));
                if (island == null && IsSameIsland(view.ViewModel.FinalIsland, islandPosition, islandMapTransition.BlueprintId))
                {
                    island = view.m_FinalIslandItemView;
                }

                if (island?.ViewModel == null)
                {
                    _logger.LogError("Unable to find island. Position={Position}, BlueprintId={BlueprintId}", islandMapTransition.Position, islandMapTransition.BlueprintId);
                    return;
                }

                island.OnSelect();
                _logger.LogInformation("Map island has been chosen. Position={Position}, BlueprintId={BlueprintId}", islandMapTransition.Position, islandMapTransition.BlueprintId);
            });
        }

        public void ChooseTransitionMapEntry(string entryId)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.TransitionPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to choose entry due to missing TransitionPCView");
                    return;
                }
                var part = view.m_Parts.FirstOrDefault(x => x.Map == view.ViewModel.Map);
                var entry = part.Entries.FirstOrDefault(x => string.Equals(x.EntranceEntry.AssetGuid.ToString(), entryId, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    _logger.LogError("Unable to find transition entry. EntryId={EntryId}", entryId);
                    return;
                }

                entry.m_LegendButton.OnLeftClick.Invoke();
                _logger.LogInformation("Transition Entry has been chosen. EntryId={EntryId}", entryId);
            });
        }

        public void CloseTransitionMap()
        {
            _mainThreadAccessor.Post(() =>
            {
                _uiAccessor.TransitionPCView?.ViewModel?.Close();
                _uiAccessor.MapIslandsPCView?.ViewModel?.Close();
                _logger.LogInformation("Transition Map has been closed");
            });
        }

        public void ReadItem(NetworkItem networkItem)
        {
            _mainThreadAccessor.Post(() =>
            {
                var owner = EntityService.Instance.GetEntity(networkItem.CollectionOwnerRef);
                var items = owner switch
                {
                    UnitEntityData unit => unit.Inventory,
                    Player player => player.Inventory,
                    DroppedLoot.EntityData lootbag => lootbag.Loot,
                    MapObjectEntityData mapObject => (mapObject.Interactions?.FirstOrDefault(i => i is InteractionLootPart) as InteractionLootPart)?.Loot,
                    _ => null
                };

                if (items == null)
                {
                    _logger.LogWarning("Unable to find item collection to read item. ItemId={ItemId}, ItemName={ItemName}", networkItem.UniqueId, networkItem.Name);

                    // every 'read' action is tied to main player, so let's use this as a last resort
                    items = Game.Instance.Player.Inventory;
                }

                var item = items.FirstOrDefault(i => IsSameUnholdedItem(i, networkItem) && i.Get<ItemPartShowInfoCallback>() != null);
                if (item == null)
                {
                    _logger.LogError("Unable to find item to read. ItemId={ItemId}, ItemName={ItemName}", networkItem.UniqueId, networkItem.Name);
                    return;
                }

                item.Get<ItemPartShowInfoCallback>().OnShowInfo();
                _logger.LogInformation("Item entity has been read. ItemId={ItemId}, ItemName={ItemName}", item.UniqueId, item.NameForAcronym);
            });
        }

        public void CopyInventoryItem(NetworkItemCopy itemCopy)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = _gameStateLookupService.GetUnitEntity(itemCopy.UnitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to copy item due to missing unit. UnitId={UnitId}", itemCopy.UnitId);
                    return;
                }

                var item = unit.Inventory.FirstOrDefault(x => IsSameItem(x, itemCopy.Item) && x.Blueprint.GetComponent<CopyItem>() != null);
                if (item == null)
                {
                    _logger.LogError("Unable to find valid item to copy. UnitId={UnitId}, ItemId={ItemId}, ItemName={ItemName}", itemCopy.UnitId, itemCopy.Item.UniqueId, itemCopy.Item.Name);
                    return;
                }

                var copyComponent = item.Blueprint.GetComponent<CopyItem>();
                copyComponent.Copy(item, unit);
                UISoundController.Instance.Play(UISoundType.SubscribeItem);
                RefreshInventoryWindow();

                _logger.LogInformation("Item has been copied. UnitId={UnitId}, ItemId={ItemId}, ItemName={ItemName}", itemCopy.UnitId, item.UniqueId, item.NameForAcronym);
            });
        }

        public void ActivateTrap(string unitId, NetworkMapObject trapObject)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var mapObject = _gameStateLookupService.GetMapObject(trapObject.Id);
                    if (mapObject == null)
                    {
                        _logger.LogWarning("Unable to find trap to activate. TrapId={TrapId}", trapObject.Id);
                        return;
                    }

                    if (mapObject is not TrapObjectData trapData)
                    {
                        _logger.LogError("Specified mapObject is not a trap. TrapId={TrapId}", trapObject.Id);
                        return;
                    }

                    if (!trapData.TrapActive)
                    {
                        return;
                    }

                    var triggeredUnit = _gameStateLookupService.GetUnitEntity(unitId);
                    if (triggeredUnit == null)
                    {
                        _logger.LogError("Unable to find unit who triggered trap. UnitId={UnitId}", unitId);
                        return;
                    }

                    EventBus.RaiseEvent<ITrapActivationHandler>(x => x.HandleTrapActivation(triggeredUnit, trapData.View));
                    trapData.RunTrapActions();
                    trapData.Deactivate(false);
                    trapData.View.PostSoundEvent(trapData.Settings.TriggerSound);
                    _logger.LogInformation("Trap has been activated. TrapId={TrapId}, UnitId={UnitId}", trapData.UniqueId, unitId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while activating trap. TrapId={TrapId}, UnitId={UnitId}", trapObject.Id, unitId);
                    throw;
                }
            });
        }

        public void ApplyTrapDisarm(NetworkTrapDisarm trapDisarm)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var mapObject = _gameStateLookupService.GetMapObject(trapDisarm.MapObject.Id);
                    if (mapObject == null)
                    {
                        _logger.LogWarning("Unable to find trap to apply disarm check. It's either already disabled or invalid id. TrapId={TrapId}", trapDisarm.MapObject.Id);
                        return;
                    }

                    if (mapObject is not TrapObjectData trapData)
                    {
                        _logger.LogError("Trap disarm can't be applied to a non-TrapObjectData. TrapId={TrapId}", trapDisarm.MapObject.Id);
                        return;
                    }

                    if (!trapData.TrapActive)
                    {
                        return;
                    }

                    var unit = _gameStateLookupService.GetUnitEntity(trapDisarm.UnitId);
                    if (unit == null)
                    {
                        _logger.LogError("Unable to find unit who interacted with a trap. TrapId={TrapId}, UnitId={UnitId}", trapDisarm.MapObject.Id, trapDisarm.UnitId);
                        return;
                    }

                    // copy-paste of TrapObjectData.Interact
                    if (trapDisarm.IsSuccess)
                    {
                        trapData.RunDisableActions(unit);
                        trapData.View.PostSoundEvent(trapData.Settings.DisabledSound);
                        trapData.Deactivate(true);
                        EventBus.RaiseEvent<IDisarmTrapHandler>(x => x.HandleDisarmTrapSuccess(unit, trapData.View));
                        return;
                    }

                    if (trapDisarm.Roll <= trapData.DisableDC - trapData.DisableTriggerMargin)
                    {
                        EventBus.RaiseEvent<IDisarmTrapHandler>(x => x.HandleDisarmTrapCriticalFail(unit, trapData.View));
                        trapData.TriggerTrap(unit);
                        return;
                    }

                    trapData.View.PostSoundEvent(trapData.Settings.DisableFailSound);
                    EventBus.RaiseEvent<IDisarmTrapHandler>(x => x.HandleDisarmTrapFail(unit, trapData.View));
                    _logger.LogWarning("Trap disarm roll has been applied. TrapId={TrapId}, Roll={Roll}, DC={DC}", trapDisarm.MapObject.Id, trapDisarm.Roll, trapData.DisableDC);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while applying trap disarm. TrapId={TrapId}", trapDisarm.MapObject.Id);
                    throw;
                }
            });
        }

        public NetworkContentState GetInstalledContent()
        {
            var state = new NetworkContentState
            {
                GameVersion = GameVersion.Cached,
                DLCs = GetInstalledDLCs(),
                Mods = GetInstalledMods()
            };

            return state;
        }

        public NetworkArea GetCurrentArea()
        {
            var currentArea = Game.Instance.CurrentlyLoadedArea;
            if (currentArea == null)
            {
                return null;
            }

            var area = new NetworkArea
            {
                Id = currentArea.AssetGuid.ToString(),
                Name = currentArea.name,
                IsGlobalMap = currentArea.IsGlobalMap,
                Chapter = Game.Instance.Player?.Chapter ?? -1
            };

            return area;
        }

        public void CreateAndEquipPolymorphicItem(NetworkPolymorphicItem polymorphicItem, bool createContext)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = _gameStateLookupService.GetUnitEntity(polymorphicItem.UnitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to create and equip polymorphic item due to missing unit. UnitId={UnitId}", polymorphicItem.UnitId);
                    return;
                }

                // InventoryStashVM.GetSmartItem
                var smartItem = unit.Inventory.Items.FirstOrDefault(i => i.Blueprint is BlueprintHiddenItem);
                if (smartItem == null)
                {
                    _logger.LogError("Unable to create and equip polymorphic item due to missing smart item. UnitId={UnitId}", polymorphicItem.UnitId);
                    return;
                }

                var polymorpItems = GetPolymorphItems(smartItem);
                var targetItem = polymorpItems.FirstOrDefault(i => string.Equals(i.NameForAcronym, polymorphicItem.Item.Name));
                if (targetItem == null)
                {
                    _logger.LogError("Unable to create and equip polymorphic item due to missing smart item variation. UnitId={UnitId}, ItemName={ItemName}", polymorphicItem.UnitId, polymorphicItem.Item.Name);
                    return;
                }

                var itemSlot = GetItemSlot(polymorphicItem.UnitId, polymorphicItem.Position);
                if (itemSlot == null)
                {
                    _logger.LogError("Unable to create and equip polymorphic item due to invalid item slot position. UnitId={UnitId}, SlotIndex={SlotIndex}, SlotType={SlotType}", polymorphicItem.UnitId, polymorphicItem.Position.Index, polymorphicItem.Position.Type);
                    return;
                }

                using var context = createContext ? _networkExecutionContext.Value = RemoteExecutionContext.Create(polymorphicItem) : null;

                var itemPolymorphPart = smartItem.Parts.Get<ItemPolymorph.ItemPolymorphPart>();
                itemPolymorphPart.CreateAndEquipPolymorphInSlot(targetItem, unit, itemSlot);
                _logger.LogInformation("Polymorphic item has been created and equipped. UnitId={UnitId}, ItemName={ItemName}, SlotType={SlotType}", unit.UniqueId, targetItem.NameForAcronym, itemSlot.GetType().Name);
            });
        }

        public void SkipCutscene(string playerName)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (CutsceneController.Skipping || CutsceneController.s_ShouldStartSkipping)
                {
                    return;
                }

                CutsceneController.SkipCutscene();
                _playerNotificationService.ShowWarningNotification(WellKnownKeys.GameNotifications.Cutscenes.Skipped.Key, args: playerName);
            });
        }

        public void ReselectSelectedCharacters()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (!Game.Instance.Player.IsInGame)
                {
                    return;
                }

                var selectionManager = Game.Instance.UI.SelectionManager as SelectionManagerPC;
                var selectedCharacters = selectionManager?.SelectedUnits.Select(x => x.View).ToList() ?? [];
                selectionManager?.MultiSelect(selectedCharacters, false);
            });
        }

        private void UpdateIslandsTransitionMap(MapIslandsPCView view, bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            view.m_CloseButton.Interactable = isInteractable;

            foreach (var island in view.m_IslandsItemViews)
            {
                if (island.ViewModel?.m_IslandState == null)
                {
                    continue;
                }

                island.m_IslandButton.Interactable = !view.ViewModel.IsTraveling.Value && island.ViewModel.m_IslandState.MayBeVisitedNow && isInteractable;
            }

            if (view.m_FinalIslandItemView.ViewModel?.m_IslandState != null)
            {
                view.m_FinalIslandItemView.m_IslandButton.Interactable = !view.ViewModel.IsTraveling.Value && view.m_FinalIslandItemView.ViewModel.m_IslandState.MayBeVisitedNow && isInteractable;
            }

            _logger.LogInformation("Island Map transitikon state has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
        }

        private void UpdateCapitalCityTransitionMap(TransitionPCView view, bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            var part = view.m_Parts.FirstOrDefault(p => p.Map == view.ViewModel.Map);
            foreach (var entry in part.Entries)
            {
                entry.m_LegendButton.Interactable = isInteractable;
                entry.m_MapButton.Interactable = isInteractable;
            }

            part.Close.Interactable = isInteractable;
            var pointerTrigger = part.Close.GetComponent<ObservablePointerClickTrigger>();
            if (pointerTrigger != null)
            {
                pointerTrigger.enabled = isInteractable;
            }

            _logger.LogInformation("Transition UI state has been updated. Map={Map}, IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", part.Map, isInteractable, readyPlayersCount, totalPlayersCount);
        }

        /// <summary>
        /// raw decompiled code of InventorySmartItemVM.GetPolymorphItems
        /// </summary>
        /// <param name="smartItemEntity"></param>
        /// <returns></returns>
        private List<ItemEntity> GetPolymorphItems(ItemEntity smartItemEntity)
        {
            var list = new List<ItemEntity>();
            ItemPolymorph.ItemPolymorphPart itemPolymorphPart = smartItemEntity.Parts.Get<ItemPolymorph.ItemPolymorphPart>();
            if (itemPolymorphPart == null)
            {
                return list;
            }
            HashSet<BlueprintItemReference> polymorphItems = itemPolymorphPart.PolymorphItems;
            IEnumerable<BlueprintItem> enumerable;
            if (polymorphItems == null)
            {
                enumerable = null;
            }
            else
            {
                enumerable = from i in polymorphItems
                             select i.Get() into i
                             where i != null
                             select i;
            }
            IEnumerable<BlueprintItem> enumerable2 = enumerable;
            if (enumerable2 == null)
            {
                return list;
            }
            list.AddRange(from i in enumerable2
                          select i.CreateEntity() into i
                          where UIUtilityItem.GetEquipPossibility(i)[0]
                          select i);
            foreach (ItemEntity itemEntity in list)
            {
                itemEntity.Identify();
            }
            return list;
        }

        private OvertipViewBase FindOvertipForObject(MapObjectEntityData mapObject)
        {
            foreach (var kv in OvertipsView.Instance.m_Views)
            {
                var view = kv.Value.FirstOrDefault(v => string.Equals(v.m_ObjectView?.Data.UniqueId, mapObject.UniqueId, StringComparison.OrdinalIgnoreCase));
                if (view != null)
                {
                    return view;
                }
            }

            return null;
        }

        private bool TryFindRequiredItemsInCollection(ItemsCollection collection, List<NetworkItem> items, out Dictionary<NetworkItem, List<ItemEntity>> matchedItems)
        {
            matchedItems = [];
            foreach (var item in items)
            {
                var existingItems = collection.Items.Where(x => IsSameUnholdedItem(x, item)).ToList();
                var existingItemsCount = existingItems.Sum(x => x.Count);
                if (existingItemsCount < item.Count)
                {
                    matchedItems = null;
                    return false;
                }

                matchedItems.Add(item, existingItems);
            }


            return true;
        }

        private ItemSlot GetItemSlot(string unitId, NetworkEquipmentSlotPosition position)
        {
            var unit = _gameStateLookupService.GetUnitEntity(unitId);

            return GetItemSlot(unit, position);
        }

        private ItemSlot GetItemSlot(UnitEntityData unit, NetworkEquipmentSlotPosition position)
        {
            if (unit == null || position == null)
            {
                return null;
            }

            var slotType = _equipmentDefinitions.GetSlotType(position.Type);

            var slotsOfSameType = unit.Body.EquipmentSlots
                    .Where(s => s.GetType() == slotType)
                    .ToList();

            if (slotsOfSameType.Count < position.Index)
            {
                return null;
            }

            var itemSlot = slotsOfSameType[position.Index];
            return itemSlot;
        }

        private Kingmaker.Utility.TargetWrapper CreateTargetWrapper(NetworkTargetWrapper networkTargetWrapper)
        {
            if (networkTargetWrapper == null)
            {
                return null;
            }

            var point = networkTargetWrapper.Point.ToUnityVector3();
            var unit = _gameStateLookupService.GetUnitEntity(networkTargetWrapper.UnitId);
            var wrapper = new Kingmaker.Utility.TargetWrapper(point, networkTargetWrapper.Orientation, unit);
            return wrapper;
        }

        private ContextData<ItemsCollection.SwapItems> CreateSwapContext(UnitEntityData unit, NetworkEquipmentSwapContext swapContext)
        {
            if (swapContext == null)
            {
                return null;
            }

            var from = GetItemSlot(unit, swapContext.From);
            var to = GetItemSlot(unit, swapContext.To);

            var context = ContextData<ItemsCollection.SwapItems>.Request().Setup(from, to);
            return context;
        }

        private void MatchSameNumberOfItems(List<ItemEntity> possibleItemsBag, int countToMatch, Action<ItemEntity> onMatched)
        {
            var itemsLeft = countToMatch;
            foreach (var item in possibleItemsBag)
            {
                var difference = itemsLeft - item.Count;
                if (difference == 0)
                {
                    onMatched(item);
                    break;
                }
                else if (difference < 0)
                {
                    var itemToDrop = item.Split(itemsLeft);
                    onMatched(itemToDrop);
                    break;
                }
                else
                {
                    // less than needed
                    itemsLeft = difference;
                    onMatched(item);
                }
            }
        }

        private bool IsSameIsland(MapIslandItemVM islandsVM, Vector2Int position, string blueprintId)
        {
            var same = islandsVM?.m_IslandState != null && islandsVM.m_IslandState.Position == position && string.Equals(islandsVM.m_IslandState.Blueprint.AssetGuid.ToString(), blueprintId, StringComparison.OrdinalIgnoreCase);
            return same;
        }

        private void DropItem(ItemsCollection inventory, ItemEntity itemEntity, string ownerId)
        {
            var itemId = itemEntity.UniqueId;
            using var context = _networkExecutionContext.Value = RemoteExecutionContext.CreateDropItem(itemId, ownerId);
            inventory.DropItem(itemEntity);
            _logger.LogInformation("Item has been dropped. EntityId={EntityId}, ItemId={ItemId}, Count={Count}", ownerId, itemId, itemEntity.Count);
        }

        private List<NetworkDLC> GetInstalledDLCs()
        {
            var dlcs = new List<NetworkDLC>();
            foreach (var dlc in BlueprintRoot.Instance.DlcSettings.Dlcs)
            {
                if (string.Equals(dlc.name, "DlcSeasonPass", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(dlc.name, "DlcPreorderAndCommanderPack", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var title = !string.IsNullOrEmpty(dlc.DefaultTitle) ? dlc.DefaultTitle
                    : (dlc.Rewards.FirstOrDefault(x => !string.IsNullOrEmpty((x as BlueprintDlcRewardCampaign)?.Campaign?.Title)) as BlueprintDlcRewardCampaign)?.Campaign.Title
                        ?? dlc.Rewards.FirstOrDefault(x => !string.IsNullOrEmpty(x.Description))?.Description
                        ?? dlc.Description;

                var networkDlc = new NetworkDLC
                {
                    Id = dlc.name,
                    IsAvailable = dlc.IsAvailable,
                    Title = title,
                };

                dlcs.Add(networkDlc);
            }

            return dlcs;
        }

        private List<NetworkMod> GetInstalledMods()
        {
            var allMods = new List<NetworkMod>();

            var unityMods = UnityModManager.modEntries.Select(x => new NetworkMod { Id = x.Info.Id, Version = x.Version.ToString(), IsEnabled = x.Enabled, Type = NetworkModType.UnityModManager }).ToList();
            allMods.AddRange(unityMods);

            var owlcatModifications = OwlcatModificationsManager.Instance
                .m_Modifications?
                .Select(x => new NetworkMod { Id = x.Manifest?.UniqueName, Type = NetworkModType.OwlcatModification, IsEnabled = true, Version = x.Manifest?.Version })
                .ToList();
            allMods.AddRange(owlcatModifications);

            return allMods;
        }

        private void UpdateRestPortraits(RestRolesPortraitsPCView view, bool isInteractable)
        {
            view.m_PrimaryUnitButton.Interactable = isInteractable;
            view.m_SecondaryUnitButton.Interactable = isInteractable;
        }

        private IEnumerable<ItemsCollection> GetLootableEntitiesInventory(NetworkLootableEntity lootableEntity)
        {
            if (lootableEntity == null)
            {
                return [];
            }

            switch (lootableEntity.Type)
            {
                case NetworkLootableEntityType.Unit:
                    var unit = _gameStateLookupService.GetUnitEntity(lootableEntity.Id);
                    if (unit == null)
                    {
                        _logger.LogError("Unable to find unit to loot. UnitId={UnitId}", lootableEntity.Id);
                        return [];
                    }

                    return [unit.Inventory];
                case NetworkLootableEntityType.Player:
                    return [Game.Instance.Player.Inventory];
                case NetworkLootableEntityType.MainStash:
                    return [Game.Instance.Player.GetSharedStash(Player.SharedStashType.MAIN)];
                case NetworkLootableEntityType.MemoriesStash:
                    return [Game.Instance.Player.GetSharedStash(Player.SharedStashType.MEMORIES)];
                case NetworkLootableEntityType.BesmaritesStash:
                    return [Game.Instance.Player.GetSharedStash(Player.SharedStashType.BESMARITES)];
                case NetworkLootableEntityType.MapObject:
                default:
                    var mapObject = _gameStateLookupService.GetMapObject(lootableEntity.Id);
                    var lookupTargets = mapObject != null ? [mapObject]
                        : _gameStateLookupService.GetNeareastLootableMapObjects(lootableEntity.Position);

                    var mapObjectContainers = lookupTargets.Select(x => ((InteractionLootPart)x.Interactions.FirstOrDefault(i => i is InteractionLootPart)).Loot);
                    return mapObjectContainers;
            }
        }

        private MechanicActionBarSlot LoadActionBarSlot(UnitEntityData unit, NetworkActionBarSlot networkActionBarSlot)
        {
            if (networkActionBarSlot.ActivatableAbility != null)
            {
                var activatableAbility = FindActivatableAbility(unit, networkActionBarSlot.ActivatableAbility);
                if (activatableAbility == null)
                {
                    _logger.LogError("Unable to find activatable ability slot content. UnitId={UnitId}, AbilityId={AbilityId}, AbilityName={AbilityName}", unit.UniqueId, networkActionBarSlot.ActivatableAbility.Id, networkActionBarSlot.ActivatableAbility.Name);
                    return null;
                }

                var activatableAbilitySlot = new MechanicActionBarSlotActivableAbility { ActivatableAbility = activatableAbility, Unit = unit };
                return activatableAbilitySlot;
            }

            if (networkActionBarSlot.Item != null)
            {
                var itemSlot = unit.Body.QuickSlots.FirstOrDefault(s => s.HasItem && IsSameItem(s.Item, networkActionBarSlot.Item));
                if (itemSlot == null)
                {
                    _logger.LogError("Unable to find item slot content. UnitId={UnitId}, ItemId={ItemId}, ItemName={ItemName}", unit.UniqueId, networkActionBarSlot.Item.UniqueId, networkActionBarSlot.Item.Name);
                    return null;
                }

                var itemActionBarSlot = new MechanicActionBarSlotItem { Item = itemSlot.Item, Unit = unit };
                return itemActionBarSlot;
            }

            var ability = _gameStateLookupService.FindAbility(unit, networkActionBarSlot.Ability);
            if (ability == null)
            {
                _logger.LogError("Unable to find ability/spell slot content. UnitId={UnitId}, AbilityId={AbilityId}, AbilityName={AbilityName}", unit.UniqueId, networkActionBarSlot.Ability.Id, networkActionBarSlot.Ability.Name);
                return null;
            }

            if (ability.Spellbook == null)
            {
                var abilitySpellSlot = new MechanicActionBarSlotAbility { Ability = ability, Unit = unit };
                return abilitySpellSlot;
            }

            if (ability.SpellSlot == null)
            {
                if (!string.IsNullOrEmpty(networkActionBarSlot.Ability.ConvertedFromId))
                {
                    var convertedSpellSlot = new MechanicActionBarSlotSpontaneusConvertedSpell { Spell = ability, Unit = unit };
                    return convertedSpellSlot;
                }

                var spontaneousSpell = new MechanicActionBarSlotSpontaneousSpell(ability) { Unit = unit };
                return spontaneousSpell;
            }

            var spellSlot = new MechanicActionBarSlotMemorizedSpell(ability.SpellSlot) { Unit = unit };
            return spellSlot;
        }

        private bool CanCancelRest(bool isInteractable)
        {
            if (Game.Instance.RestController.CurrentPhase == RestPhase.ShowingResults)
            {
                return false;
            }

            if (Game.Instance.Player.CapitalPartyMode || RootUIContext.Instance.IsGlobalMap)
            {
                return isInteractable;
            }

            return true;
        }

        private void RefreshSpellbookUI()
        {
            _uiAccessor.SpellbookPCView?.ViewModel?.UpdateSpellbook();
        }

        private AbilityData GetDuplicateCustomSpell(Spellbook spellbook, AbilityData newMetamagicSpell)
        {
            var spellLevel = spellbook.GetSpellLevel(newMetamagicSpell);
            var customSpellLevelSpells = spellbook.GetCustomSpells(spellLevel);
            foreach (AbilityData customSpell in customSpellLevelSpells)
            {
                if (customSpell.Blueprint == newMetamagicSpell.Blueprint && Equals(customSpell.MetamagicData, newMetamagicSpell.MetamagicData))
                {
                    return customSpell;
                }
            }

            return null;
        }

        private void RefreshVendorScreen()
        {
            if (_uiAccessor.VendorViewVM == null)
            {
                _logger.LogWarning("Unable to refresh vendor screen due to missing VendorViewVM");
                return;
            }

            try
            {
                _uiAccessor.VendorViewVM.UpdateVendorSide();
                _uiAccessor.VendorViewVM.UpdatePlayerSide();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while refreshing vendor screen");
                throw;
            }
        }

        private void AddItemToVendorSellCollection(NetworkVendorItemTransfer transfer)
        {
            var possibleItems = Game.Instance.Player.Inventory.Where(i => IsSameItem(i, transfer.Item) &&
                (string.IsNullOrEmpty(transfer.Item.HoldingSlotOwnerId) && i.HoldingSlot == null ||
                    !string.IsNullOrEmpty(transfer.Item.HoldingSlotOwnerId) && string.Equals(i.HoldingSlot?.Owner?.Unit.UniqueId, transfer.Item.HoldingSlotOwnerId, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => x.Count)
                .ToList();

            MatchSameNumberOfItems(possibleItems, transfer.Count,
                item =>
                {
                    using var context = _networkExecutionContext.Value = RemoteExecutionContext.CreateVendorItemTransfer(item.UniqueId);
                    Game.Instance.Vendor.AddForSell(item, item.Count);
                    _logger.LogInformation("Vendor item has been transferred. ItemId={ItemId}, Count={Count}, ActionTarget={ActionTarget}, ActionType={ActionType}", item.UniqueId, item.Count, transfer.ItemActionTarget, transfer.ItemAction);
                });
        }

        private (ItemEntity itemEntity, Func<ItemEntity, ItemEntity> vendorAction) GetDataForVendorTransferAction(NetworkItem networkItem, int count, VendorItemActionTarget target, VendorItemAction itemAction)
        {
            (ItemsCollection source, Func<ItemEntity, ItemEntity> vendorAction) = target switch
            {
                VendorItemActionTarget.Sell when itemAction == VendorItemAction.Remove => Tuple.Create<ItemsCollection, Func<ItemEntity, ItemEntity>>(Game.Instance.Vendor.ItemsForSell, i => Game.Instance.Vendor.RemoveFromSell(i, count)),
                VendorItemActionTarget.Buy when itemAction == VendorItemAction.Add => Tuple.Create<ItemsCollection, Func<ItemEntity, ItemEntity>>(Game.Instance.Vendor.StoreItems, i => Game.Instance.Vendor.AddForBuy(i, count)),
                VendorItemActionTarget.Buy when itemAction == VendorItemAction.Remove => Tuple.Create<ItemsCollection, Func<ItemEntity, ItemEntity>>(Game.Instance.Vendor.ItemsForBuy, i => Game.Instance.Vendor.RemoveFromBuy(i, count)),
                _ => Tuple.Create<ItemsCollection, Func<ItemEntity, ItemEntity>>(null, null),
            };

            if (source == null)
            {
                _logger.LogError("Unable to find correct vendor item source. Target={Target}, ItemAction={ItemAction}", target, itemAction);
                return (null, null);
            }

            var item = source.Items.FirstOrDefault(i => IsSameItem(i, networkItem));
            return (item, vendorAction);
        }

        private CraftItemInfo GetCampingCraftItemInfo(UnitEntityData crafter, UsableItemType itemType, string itemBlueprintId)
        {
            if (crafter == null)
            {
                return null;
            }

            var craftingRecipes = Game.Instance.BlueprintRoot.CraftRoot.CollectCraftList(crafter, itemType);
            var recipe = craftingRecipes.FirstOrDefault(c => string.Equals(c.Info.Item.AssetGuid.ToString(), itemBlueprintId, StringComparison.OrdinalIgnoreCase));

            return recipe?.Info;
        }

        private void UpdateScrollScribingRecipe(CampingState campingState, string scrollItemBlueprintId)
        {
            if (string.IsNullOrEmpty(scrollItemBlueprintId))
            {
                campingState.SelectedScroll = null;
                return;
            }

            if (campingState.SelectedScroll != null && string.Equals(campingState.SelectedScroll.Item.AssetGuid.ToString(), scrollItemBlueprintId))
            {
                _logger.LogInformation("Same camping scroll scribing recipe is already selected, skipping updates. BlueprintId={BlueprintId}", scrollItemBlueprintId);
                return;
            }

            var roleType = CampingRoleType.ScrollScribe;
            var crafter = campingState.GetCharacterByRoleType(roleType);
            if (crafter == null)
            {
                return;
            }

            var recipe = GetCampingCraftItemInfo(crafter, UsableItemType.Scroll, scrollItemBlueprintId);
            if (recipe == null)
            {
                _logger.LogError("Unable update camping scroll scribing recipe due to missing blueprint id. BlueprintId={BlueprintId}", scrollItemBlueprintId);
                return;
            }

            campingState.SelectedScroll = recipe;

            UpdateCraftingUI(roleType);
        }

        private void UpdateAlchemistRecipe(CampingState campingState, string potionBlueprintRecipeId)
        {
            if (string.IsNullOrEmpty(potionBlueprintRecipeId))
            {
                campingState.SelectedPotion = null;
                return;
            }

            if (campingState.SelectedPotion != null && string.Equals(campingState.SelectedPotion.Item.AssetGuid.ToString(), potionBlueprintRecipeId))
            {
                _logger.LogInformation("Same camping alchemist potion recipe is already selected, skipping updates. BlueprintId={BlueprintId}", potionBlueprintRecipeId);
                return;
            }

            var roleType = CampingRoleType.Alchemist;
            var crafter = campingState.GetCharacterByRoleType(roleType);
            if (crafter == null)
            {
                return;
            }

            var recipe = GetCampingCraftItemInfo(crafter, UsableItemType.Potion, potionBlueprintRecipeId);
            if (recipe == null)
            {
                _logger.LogError("Unable update camping alchemist potion recipe due to missing blueprint id. BlueprintId={BlueprintId}", potionBlueprintRecipeId);
                return;
            }

            campingState.SelectedPotion = recipe;

            UpdateCraftingUI(roleType);
        }

        private void UpdateCraftingUI(CampingRoleType campingRoleType)
        {
            EventBus.RaiseEvent<IRestRoleUIStageEvents>(x => x.RestStageClosed(campingRoleType, CraftStage.CraftFirstType, true));
        }

        private void UpdateCookingRecipe(CampingState campingState, string cookingBlueprintRecipeId)
        {
            var cookingRecipe = string.IsNullOrEmpty(cookingBlueprintRecipeId) ? null : campingState.KnownRecipes.FirstOrDefault(r => string.Equals(r.AssetGuid.ToString(), cookingBlueprintRecipeId, StringComparison.OrdinalIgnoreCase));
            campingState.CookingRecipe = cookingRecipe;
        }

        private void RefreshInventoryWindow()
        {
            _uiAccessor.InventoryVM?.StashVM?.CollectionChanged();
            _uiAccessor.InventoryVM?.DollVM?.RefreshData();
        }

        private bool IsSameUnholdedItem(ItemEntity itemEntity, NetworkItem networkLootItem)
        {
            // dead units retain it's holding state
            return ((itemEntity.Owner?.State?.IsDead ?? true) || itemEntity.HoldingSlot == null) && IsSameItem(itemEntity, networkLootItem);
        }

        private bool IsSameItem(ItemEntity itemEntity, NetworkItem networkLootItem)
        {
            var sameItemType = string.Equals(itemEntity.NameForAcronym, networkLootItem.Name, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(itemEntity.UniqueId, networkLootItem.UniqueId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(itemEntity.Blueprint.AssetGuid.ToString(), networkLootItem.BlueprintId, StringComparison.OrdinalIgnoreCase))
                && itemEntity.Cost == networkLootItem.Cost
                && itemEntity.IsLootable
                && itemEntity.EnchantmentValue == networkLootItem.EnchantmentValue
                && AreSameEnchantments(itemEntity.Enchantments, networkLootItem.Enchantments);

            return sameItemType;
        }

        private bool AreSameEnchantments(List<ItemEnchantment> itemEnchantments, List<string> enchantments)
        {
            var same = itemEnchantments.Count == enchantments.Count
                && enchantments.All(x => itemEnchantments.Any(i => string.Equals(i.Blueprint.name, x, StringComparison.OrdinalIgnoreCase)));
            return same;
        }

        private void RefreshLootUI()
        {
            var lootVm = Game.Instance.RootUiContext?.InGameVM?.StaticPartVM?.LootContextVM?.LootVM?.Value;
            if (lootVm != null)
            {
                foreach (var lootObjectVM in lootVm.ContextLoot)
                {
                    lootObjectVM.UpdateCommand.Execute();
                }
            }

            _uiAccessor.LootPCView.ViewModel?.InventoryCollectionChanged();
        }

        private ActivatableAbility FindActivatableAbility(UnitEntityData caster, NetworkActivatableAbility activatableAbility)
        {
            if (activatableAbility.ShifterFuryIndex >= 0)
            {
                var shifterFuryPart = caster.Get<ShiftersFuryPart>();
                if (shifterFuryPart != null && shifterFuryPart.AppliedFacts != null && shifterFuryPart.AppliedFacts.Count > activatableAbility.ShifterFuryIndex)
                {
                    var shifterAbility = shifterFuryPart.AppliedFacts[activatableAbility.ShifterFuryIndex];
                    _logger.LogInformation("Shifter fury abiliy has been found. UnitId={UnitId}, Id={Id}, Name={Name}, Index={Index}", caster.UniqueId, shifterAbility.UniqueId, shifterAbility.NameForAcronym, activatableAbility.ShifterFuryIndex);
                    return shifterAbility;
                }
            }

            var abilities = caster.ActivatableAbilities?.Enumerable ?? [];
            var ability = abilities.FirstOrDefault(a => string.Equals(a.UniqueId, activatableAbility.Id, StringComparison.OrdinalIgnoreCase));
            if (ability == null)
            {
                var sameBlueprint = abilities.Where(x => string.Equals(x.Blueprint.AssetGuid.ToString(), activatableAbility.BlueprintId)).ToList();
                if (sameBlueprint.Count == 1)
                {
                    ability = sameBlueprint.FirstOrDefault();
                    return ability;
                }
            }

            return ability;
        }

        private void ExecuteClickHandler(IClickEventHandler clickEventHandler, NetworkClick click)
        {
            var targetUnit = _gameStateLookupService.GetUnitEntity(click.TargetUnitId);
            var selectedUnits = click.SelectedUnits.Select(_gameStateLookupService.GetUnitEntity).ToList();
            var selectedUnit = selectedUnits.FirstOrDefault();
            var worldPosition = click.WorldPosition.ToUnityVector3();

            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    _logger.LogInformation("Executing click handler. HandlerType={HandlerType}, WorldPosition={WorldPosition}, TargetUnitId={TargetUnitId}, SelectedUnit={SelectedUnit}",
                               clickEventHandler.GetType().Name, click.WorldPosition, targetUnit?.UniqueId, selectedUnit?.UniqueId);

                    using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(selectedUnits);
                    Game.Instance.SelectionCharacter.SelectedUnit.Value = selectedUnit;

                    if (!string.IsNullOrEmpty(click.MovementLimit) && Game.Instance.TurnBasedCombatController.CurrentTurn != null && Game.Instance.TurnBasedCombatController.CurrentTurn.Rider != null)
                    {
                        Enum.TryParse<TurnBased.Controllers.TurnController.MovementLimit>(click.MovementLimit, true, out var limit);
                        Game.Instance.TurnBasedCombatController.CurrentTurn.SetMovementLimit(limit);
                        _logger.LogInformation("Movement limit has been updated. UnitId={UnitId}, Limit={Limit}", Game.Instance.TurnBasedCombatController.CurrentTurn.Rider.UniqueId, limit);
                    }

                    if (click.VectorPath != null && click.VectorPath.Count > 0 && PathVisualizer.Instance != null)
                    {
                        var movementPath = click.VectorPath.Select(v => v.ToUnityVector3()).ToList();
                        PathVisualizer.Instance.m_CurrentPath = new ForcedPath(movementPath);
                        PathVisualizer.Instance.m_CurrentPath.Claim(PathVisualizer.Instance);
                    }

                    clickEventHandler.OnClick(targetUnit?.View?.gameObject, worldPosition, click.Button, simulate: false, click.MuteEvents, IsTMBClick: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to execute click handler. HandlerType={HandlerType}", clickEventHandler.GetType().Name);
                    throw;
                }
            });
        }
    }
}
