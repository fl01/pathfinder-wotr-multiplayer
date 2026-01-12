using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Clicks;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.Controllers.MapObjects;
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
using Kingmaker.UI.MVVM._PCView.NewGame.Story;
using Kingmaker.UI.MVVM._PCView.Rest;
using Kingmaker.UI.MVVM._PCView.Settings.Entities.Difficulty;
using Kingmaker.UI.MVVM._VM.Lockpick;
using Kingmaker.UI.MVVM._VM.NewGame;
using Kingmaker.UI.MVVM._VM.Settings.Entities;
using Kingmaker.UI.Selection;
using Kingmaker.UI.SettingsUI;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.Utility;
using Kingmaker.View;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.Core.Utils;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UniRx;
using UnityEngine;
using UnityModManagerNet;
using WOTRMultiplayer.Abstractions.GameInteraction;
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
using WOTRMultiplayer.Entities.Movement;
using WOTRMultiplayer.Entities.NewGame;
using WOTRMultiplayer.Entities.Ping;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Entities.Units;
using WOTRMultiplayer.Entities.Vendor;
using WOTRMultiplayer.Services.GameInteraction.Contexts;
using WOTRMultiplayer.Services.Settings;
using WOTRMultiplayer.UnityBehaviours;

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

        public void MoveNonCombatCharacter(NetworkCharacterMove networkCharacterMove)
        {
            var character = Game.Instance.Player.PartyAndPets.FirstOrDefault(f => string.Equals(f.UniqueId, networkCharacterMove.UnitId, StringComparison.OrdinalIgnoreCase));
            if (character == null)
            {
                _logger.LogError("Can't move missing character. UnitId={UnitId}", networkCharacterMove.UnitId);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                var unityDestination = new Vector3(networkCharacterMove.Destination.X, networkCharacterMove.Destination.Y, networkCharacterMove.Destination.Z);
                var command = new UnitMoveTo(unityDestination, 0.3f)
                {
                    MovementDelay = networkCharacterMove.Delay,
                    Orientation = networkCharacterMove.Orientation,
                    CreatedByPlayer = true
                };
                character.Commands.Run(command);
            });
        }

        public void SetPause(bool isPaused)
        {
            _logger.LogInformation("Pause game. IsPaused={IsPaused}", isPaused);
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

        public bool IsUnitAI(string unitId)
        {
            var unit = Game.Instance.Player.PartyAndPets.FirstOrDefault(p => string.Equals(p.UniqueId, unitId, StringComparison.OrdinalIgnoreCase));
            return unit == null;
        }

        public NetworkCombatState GetCombatState()
        {
            var state = new NetworkCombatState
            {
                RoundNumber = Game.Instance.TurnBasedCombatController.RoundNumber,
                HasSurpriseRound = Game.Instance.TurnBasedCombatController.m_HasSurpriseRound,
                Units = GetUnitsInCombat()
            };

            return state;
        }

        public Task UpdateCombatStateAsync(NetworkCombatState networkCombatState, bool requiresFullUpdate)
        {
            var taskCompletion = new TaskCompletionSource<bool>();
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    _logger.LogInformation("Updating combat state");
                    var requiresExtraLog = false;
                    if (requiresFullUpdate && Game.Instance.TurnBasedCombatController.m_HasSurpriseRound != networkCombatState.HasSurpriseRound)
                    {
                        _logger.LogWarning("Surprise round difference synced. PreviousSurpriseState={PreviousSurpriseState}, NewSurpriseState={NewSurpriseState}", Game.Instance.TurnBasedCombatController.m_HasSurpriseRound, networkCombatState.HasSurpriseRound);
                        Game.Instance.TurnBasedCombatController.m_HasSurpriseRound = networkCombatState.HasSurpriseRound;
                        requiresExtraLog = true;
                    }

                    if (requiresFullUpdate && Game.Instance.TurnBasedCombatController.RoundNumber != networkCombatState.RoundNumber)
                    {
                        _logger.LogWarning("RoundNumber difference synced. PreviousRoundNumber={PreviousRoundNumber}, NewRoundNumber={NewRoundNumber}", Game.Instance.TurnBasedCombatController.RoundNumber, networkCombatState.RoundNumber);
                        Game.Instance.TurnBasedCombatController.RoundNumber = networkCombatState.RoundNumber;
                        requiresExtraLog = true;
                    }

                    UpdateCombatState(networkCombatState, requiresFullUpdate);

                    if (requiresExtraLog)
                    {
                        Game.Instance.TurnBasedCombatController.LogRound();
                    }

                    taskCompletion.SetResult(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while updating combat state");
                    throw;
                }
            });

            return taskCompletion.Task;
        }

        public string QuickLoadGame(string savePath)
        {
            var save = LoadSave(savePath);
            _mainThreadAccessor.Post(() =>
            {
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
                            using (Kingmaker.ElementsSystem.ContextData<UnitEntityData.ChargenUnit>.Request())
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
            var unit = Game.Instance.State.Units.FirstOrDefault(u => string.Equals(u.UniqueId, unitId));
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

        public void StartTurnBasedCombatTurn(string unitId)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    _logger.LogInformation("Calling CombatController.StartTurn. UnitId={UnitId}", unitId);
                    var currentUnit = _gameStateLookupService.GetUnitEntity(unitId);
                    if (currentUnit == null)
                    {
                        _logger.LogError("Unable to find unit to call CombatController.StartTurn. UnitId={UnitId}", unitId);
                        return;
                    }

                    Game.Instance.TurnBasedCombatController.StartTurn(currentUnit);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to start CombatController.StartTurn");
                    throw;
                }
            });
        }

        public void EndTurnBasedCombatTurn()
        {
            // TODO: proper command queue
            while ((Game.Instance.TurnBasedCombatController?.CurrentTurn?.Rider?.Commands?.Queue?.Count ?? 0) > 0)
            {
                Thread.Sleep(50);
            }

            _mainThreadAccessor.Post(() =>
            {
                var turnStatus = Game.Instance.TurnBasedCombatController.CurrentTurn?.Status ?? null;
                _logger.LogInformation("Ending combat turn if it's not ending yet. TurnStatus={TurnStatus}", turnStatus);
                if (turnStatus != TurnBased.Controllers.TurnController.TurnStatus.Ending && turnStatus != TurnBased.Controllers.TurnController.TurnStatus.Ended)
                {
                    Game.Instance.TurnBasedCombatController.CurrentTurn?.End();
                }
            });
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
                    var mapObject = networkClick.IsLootBagMapObject ? GetNeareastLootBagMapObject(networkClick.WorldPosition) : _gameStateLookupService.GetMapObject(networkClick.MapObjectId);
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
                var caster = _gameStateLookupService.GetUnitEntity(networkActivatableAbility.CasterId);
                if (caster == null)
                {
                    _logger.LogError("Caster of activatable ability doesn't exist. UnitId={UnitId}", networkActivatableAbility.CasterId);
                    return;
                }

                var ability = FindActivatableAbility(caster, networkActivatableAbility);
                if (ability == null)
                {
                    _logger.LogError("Unable to find activatable ability. UnitId={UnitId}, AbilityId={AbilityId}", caster.UniqueId, networkActivatableAbility.Id);
                    return;
                }

                var target = _gameStateLookupService.GetUnitEntity(networkActivatableAbility.TargetId);
                _mainThreadAccessor.Post(() =>
                {
                    ability.SetIsOn(networkActivatableAbility.IsActive, target);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiate ToggleActivatableAbility.  CasterId={CasterId}, TargetId={TargetId}, AbilityId={AbilityId}", networkActivatableAbility.CasterId, networkActivatableAbility.TargetId, networkActivatableAbility.Id);
                throw;
            }
        }

        public void UseAbility(NetworkAbility networkAbility)
        {
            try
            {
                var caster = _gameStateLookupService.GetUnitEntity(networkAbility.CasterId);
                var abilityData = FindAbility(caster, networkAbility);
                if (abilityData == null)
                {
                    _logger.LogError("Unable to find ability. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookBlueprintId={SpellbookBlueprintId}", caster.UniqueId, networkAbility.Id, networkAbility.SpellbookId);
                    return;
                }

                var target = _gameStateLookupService.GetUnitEntity(networkAbility.TargetId);
                var point = new Vector3(networkAbility.TargetPoint.X, networkAbility.TargetPoint.Y, networkAbility.TargetPoint.Z);
                var targetWrapper = new TargetWrapper(point, null, target);
                Enum.TryParse<UnitCommand.CommandType>(networkAbility.CommandType, true, out var commandType);

                _mainThreadAccessor.Post(() =>
                {
                    var command = UnitUseAbility.CreateCastCommand(abilityData, targetWrapper, commandType);
                    command.CreatedByPlayer = true;
                    if (networkAbility.VectorPath != null)
                    {
                        var movementPath = networkAbility.VectorPath.Select(v => new Vector3(v.X, v.Y, v.Z)).ToList();
                        command.ForcedPath = new ForcedPath(movementPath);
                        PathVisualizer.Instance.m_CurrentPath = command.ForcedPath;
                        PathVisualizer.Instance.m_CurrentPath.Claim(PathVisualizer.Instance);
                    }

                    _logger.LogInformation("Running ability use command. Caster={Caster}, AbilityId={AbilityId}, AbilityName={AbilityName}, ForcedPath={ForcedPath}", caster.UniqueId, abilityData.UniqueId, abilityData.NameForAcronym, command.ForcedPath?.vectorPath?.Count);
                    caster.Commands.Run(command);
                });
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Unable to initiate UseAbility.  CasterId={CasterId}, TargetId={TargetId}, AbilityId={AbilityId}", networkAbility.CasterId, networkAbility.TargetId, networkAbility.Id);
                throw;
            }
        }

        public bool CombatTurnHasBeenFinished()
        {
            var turnStatus = Game.Instance.TurnBasedCombatController.CurrentTurn?.Status ?? TurnBased.Controllers.TurnController.TurnStatus.None;
            return turnStatus == TurnBased.Controllers.TurnController.TurnStatus.None
                || turnStatus == TurnBased.Controllers.TurnController.TurnStatus.Ended
                || turnStatus == TurnBased.Controllers.TurnController.TurnStatus.Ending;
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
                        var containerItems = matchedItems.Get(item);
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
                _logger.LogError("Unable to find item to drop. EntityId={EntityId}, ItemId={ItemId}", networkDropItem.OwnerEntityId, networkDropItem.Item.UniqueId);
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
                _logger.LogInformation("Trigerring perception check. MapObjectId={MapObjectId}", networkPerceptionCheck.MapObject.Id);
                using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(networkPerceptionCheck);
                PartyPerceptionController.RollPerception(unit, mapObject);
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
                EventBus.RaiseEvent<IUnitInStealthSpottedHandler>(x => x.HandleUnitInStealthSpotted(stealhedUnitId, perceptionCheck), true);
                if (stealhedUnitId.Stealth.AddSpottedBy(initiatorUnitId))
                {
                    EventBus.RaiseEvent<IUnitSpottedHandler>(x => x.HandleUnitSpotted(stealhedUnitId, initiatorUnitId), true);
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
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.Combat.AISync.Key, networkGameSettings.Multiplayer.SyncAICombatActions);
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.RemoteRollRetrievalTimeout.Key, networkGameSettings.Multiplayer.RemoteRollRetrievalTimeout.ToString());
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.NetworkAwaiterTimeout.Key, networkGameSettings.Multiplayer.NetworkAwaiterTimeout.ToString());
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.AISyncTimeout.Key, networkGameSettings.Multiplayer.AISyncTimeout.ToString());
                    SettingsController.GeneralSettingsProvider.SetValue(WellKnownSettings.DangerZone.RestEncounterSyncTimeout.Key, networkGameSettings.Multiplayer.RestEncounterSyncTimeout.ToString());
                }
            });
        }

        public void SpawnCampPlace(NetworkVector3 position)
        {
            _mainThreadAccessor.Post(() =>
            {
                var campPosition = new Vector3(position.X, position.Y, position.Z);
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

                    _logger.LogInformation("Changing group changer view state. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);

                    _uiAccessor.GroupChangerView.m_AcceptButton.Interactable = isInteractable;
                    _uiAccessor.GroupChangerView.m_CloseButton.Interactable = isInteractable;
                    var acceptButtonText = _uiAccessor.GroupChangerView.m_AcceptButton.GetComponentInChildren<TextMeshProUGUI>();
                    _uiSyncCountersService.UpdateButtonTextCounter(acceptButtonText, readyPlayersCount, totalPlayersCount);
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
                    if (_uiAccessor.SkipTimeView?.ViewModel == null)
                    {
                        _logger.LogWarning("Skip time UI is already closed");
                        return;
                    }

                    _uiAccessor.SkipTimeView.ViewModel.Close();
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

                EventBus.RaiseEvent<ISkipTimeWindowUIHandler>(x => x.HandleOpenSkipTime(), true);
            });
        }

        public void UpdateSkipTimeHours(float hours)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (_uiAccessor.SkipTimeView?.ViewModel == null)
                    {
                        _logger.LogWarning("Unable to update skip time hours due to missing UI");
                        return;
                    }

                    _uiAccessor.SkipTimeView.m_HoursSlider.value = hours;
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
                    if (_uiAccessor.SkipTimeView.ViewModel == null)
                    {
                        _logger.LogWarning("Unable to start skip time due to missing UI");
                        return;
                    }

                    _uiAccessor.SkipTimeView.m_SkipTimeButton.OnLeftClick.Invoke();
                    _logger.LogInformation("Skip time has been started");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to start skip time");
                    throw;
                }
            });
        }

        public void UpdateStartRestButtonState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (_uiAccessor.RestView == null)
                    {
                        return;
                    }

                    _logger.LogInformation("Changing rest button state. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);

                    _uiAccessor.RestView.m_StartRestButton.Interactable = isInteractable;
                    _uiSyncCountersService.UpdateButtonTextCounter(_uiAccessor.RestView.m_StartRestButtonText, readyPlayersCount, totalPlayersCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to update start rest button state");
                    throw;
                }
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

        public void UpdateIsInCombatStatus()
        {
            Game.Instance.Player.UpdateIsInCombat();
            _mainThreadAccessor.Post(() =>
            {
                Game.Instance.Player.UpdateIsInCombat();
            });
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

        public void ForgetSpell(NetworkSpellSlot networkSpellSlot)
        {
            var unit = _gameStateLookupService.GetUnitEntity(networkSpellSlot.UnitId);
            if (unit == null)
            {
                _logger.LogError("Unable to find unit to forget spell. UnitId={UnitId}", networkSpellSlot.UnitId);
                return;
            }

            var spellbook = unit.Spellbooks.FirstOrDefault(s => string.Equals(s.Blueprint.Name.Key, networkSpellSlot.SpellbookId, StringComparison.OrdinalIgnoreCase));
            if (spellbook == null)
            {
                _logger.LogError("Unable to find spellbook to forget spell. UnitId={UnitId}, SpellbookId={SpellbookId}", networkSpellSlot.UnitId, networkSpellSlot.SpellbookId);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                var spellSlot = GetSpellSlot(spellbook, networkSpellSlot);
                if (spellSlot == null)
                {
                    _logger.LogError("Unable to find spellslot to forget. UnitId={UnitId}, SpellbookId={SpellbookId}, SpellSlotIndex={SpellSlotIndex}, SpellSlotType={SpellSlotType}", networkSpellSlot.UnitId, networkSpellSlot.SpellbookId, networkSpellSlot.Index, networkSpellSlot.Type);
                    return;
                }

                _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.SpellBook.ForgottenSpell.Key, spellSlot.SpellShell?.Name, unit.CharacterName);
                spellbook.ForgetMemorized(spellSlot);
                RefreshSpellbookUI();
            });
        }

        public void MemorizeSpell(NetworkSpellSlot networkSpellSlot)
        {
            var unit = _gameStateLookupService.GetUnitEntity(networkSpellSlot.UnitId);
            if (unit == null)
            {
                _logger.LogError("Unable to find unit to memorize spell. UnitId={UnitId}", networkSpellSlot.UnitId);
                return;
            }

            var spellbook = unit.Spellbooks.FirstOrDefault(s => string.Equals(s.Blueprint.Name.Key, networkSpellSlot.SpellbookId, StringComparison.OrdinalIgnoreCase));
            if (spellbook == null)
            {
                _logger.LogError("Unable to find spellbook to memorize spell. UnitId={UnitId}, SpellbookId={SpellbookId}", networkSpellSlot.UnitId, networkSpellSlot.SpellbookId);
                return;
            }

            _mainThreadAccessor.Post(() =>
            {
                var spellSlot = GetSpellSlot(spellbook, networkSpellSlot);
                var spell = GetKnownSpell(spellbook, networkSpellSlot.SpellId, networkSpellSlot.SpellName);
                spellbook.Memorize(spell, spellSlot);
                _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.SpellBook.MemorizedSpell.Key, spell.Name, unit.CharacterName);
                RefreshSpellbookUI();
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
                    _logger.LogError("New game sequence has been terminted");
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

        public void AttackUnit(NetworkUnitAttack attack)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var executor = _gameStateLookupService.GetUnitEntity(attack.ExecutorUnitId);
                    if (executor == null)
                    {
                        _logger.LogError("Unable to find executor unit to perform unit attack command. ExecutorUnitId={ExecutorUnitId}", attack.ExecutorUnitId);
                        return;
                    }
                    var target = _gameStateLookupService.GetUnitEntity(attack.TargetUnitId);
                    if (target == null)
                    {
                        _logger.LogError("Unable to find target unit to perform unit attack command. TargetUnitId={TargetUnitId}", attack.TargetUnitId);
                        return;
                    }

                    var command = UnitAttack.CreateAttackCommand(executor, target) as UnitAttack;
                    if (attack.IsFullAttack)
                    {
                        command.ForceFullAttack = true;
                        var turn = Game.Instance.TurnBasedCombatController.CurrentTurn;
                        if (turn != null)
                        {
                            turn.m_AttackMode = TurnBased.Controllers.TurnController.AttackMode.FullAttack;
                        }
                    }

                    var movementPath = attack.VectorPath.Select(v => new Vector3(v.X, v.Y, v.Z)).ToList();
                    command.ForcedPath = new ForcedPath(movementPath);
                    command.CreatedByPlayer = true;
                    var cd = executor.CombatState.Cooldown;
                    var combatState = Game.Instance.TurnBasedCombatController.CurrentTurn?.GetActionsStates(Game.Instance.TurnBasedCombatController.CurrentTurn.Rider);
                    _logger.LogInformation("Starting unit attack command. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, ForceFullAttack={ForceFullAttack}, SelectedUnitId={SelectedUnitId}, StandardCD={StandardAction}, MoveCD={MoveAction}, SwiftCD={SwiftAction}, InitiativeCD={Initiative}",
                        attack.ExecutorUnitId, attack.TargetUnitId, attack.IsFullAttack, Game.Instance.TurnBasedCombatController.CurrentTurn?.SelectedUnit?.UniqueId, cd.StandardAction, cd.MoveAction, cd.SwiftAction, cd.Initiative);
                    executor.Commands.Run(command);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to attack unit. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}", attack.ExecutorUnitId, attack.TargetUnitId);
                    throw;
                }
            });
        }

        public void DelayCombatTurn(string unitId, string targetUnitId)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = _gameStateLookupService.GetUnitEntity(unitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to delay combat turn due to missing unit. UnitId={UnitId}", unitId);
                    return;
                }

                var targetUnit = _gameStateLookupService.GetUnitEntity(targetUnitId);
                if (targetUnit == null)
                {
                    _logger.LogError("Unable to delay combat turn due to missing target unit. TargetUnitId={TargetUnitId}", targetUnit);
                    return;
                }

                Game.Instance.TurnBasedCombatController.HandleDelayTurn(unit, targetUnit);
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

                // looks like we can just leave zone since item collection is synced anyway
                // however, let's keep collect all just in case
                _uiAccessor.LootPCView.ViewModel.CollectAll();

                _logger.LogError("ZoneLoot has been completed");
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

        public bool IsInCombat()
        {
            return Game.Instance.Player.IsInCombat;
        }

        public bool CanRiderGetUp()
        {
            var canGetUp = Game.Instance.TurnBasedCombatController.CurrentTurn?.UnitCanGetUpOnCommand.Value ?? false;
            return canGetUp;
        }

        public bool HasAnyRunningCombatCommands()
        {
            var hasAnyCommmands = Game.Instance.TurnBasedCombatController.CurrentTurn?.m_RunningCommands.Count > 0;
            return hasAnyCommmands;
        }

        public int GetCurrentChapter()
        {
            var chapter = Game.Instance.Player?.Chapter ?? int.MaxValue;
            return chapter;
        }

        public string GetCurrentAreaName()
        {
            var areaName = Game.Instance.CurrentlyLoadedArea?.name;
            return areaName;
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

        public NetworkPing GetPing()
        {
            if (PointerController.InGui)
            {
                var guiPing = GetPingedGuiElement();
                return guiPing;
            }

            var pointer = PointerController.PointerPosition;
            Game.Instance.DefaultPointerController.SelectClickObject(pointer, out var gameObject, out var worldPosition, out _);

            if (worldPosition == Vector3.zero && gameObject == null)
            {
                return null;
            }

            var point = new NetworkVector3(worldPosition.x, worldPosition.y, worldPosition.z);
            var unitId = gameObject?.GetComponent<UnitEntityView>()?.Data?.UniqueId;
            var mapObjectData = gameObject?.GetComponent<MapObjectView>()?.Data;
            var ping = new NetworkPing
            {
                WorldPosition = point,
                UnitId = unitId,
                MapObject = mapObjectData == null ? null : new NetworkMapObject { Id = mapObjectData.UniqueId, Position = new NetworkVector3(mapObjectData.Position.x, mapObjectData.Position.y, mapObjectData.Position.z) },
            };

            if (ping.MapObject != null)
            {
                ping.Type = NetworkPingType.MapObject;
            }
            else if (!string.IsNullOrEmpty(ping.UnitId))
            {
                ping.Type = NetworkPingType.Unit;
            }
            else
            {
                ping.Type = NetworkPingType.WorldPosition;
            }

            return ping;
        }

        public void CreatePing(string playerName, NetworkPing ping)
        {
            _mainThreadAccessor.Post(() =>
            {
                // TODO: expand supported pings
                if (ping.Type != NetworkPingType.WorldPosition)
                {
                    return;
                }

                // this is a placeholder that needs to be replaced with something good
                var position = new Vector3(ping.WorldPosition.X, ping.WorldPosition.Y, ping.WorldPosition.Z);
                var pingObject = UnityEngine.Object.Instantiate(ClickPointerManager.Instance.PointerPrefab.gameObject);
                var meshRenderers = pingObject.transform.Children().SelectMany(x => x.Children()).Select(x => x.GetComponent<MeshRenderer>()).ToList();
                var even = new Color(0f, 0f, 0.6f, 1f);
                var odd = new Color(0.887f, 0.273f, 0.263f, 1f);
                for (int i = 0; i < meshRenderers.Count; i++)
                {
                    var color = i % 2 == 0 ? even : odd;
                    var renderer = meshRenderers[i];
                    renderer.material.color = color;
                }

                var clickPointer = pingObject.GetComponent<ClickPointerPrefab>();
                clickPointer.transform.SetParent(ClickPointerManager.Instance.transform);
                clickPointer.transform.localPosition = position;

                var decayingBehaviour = pingObject.AddComponent<DecayingMeshRenderersBehaviour>();
                decayingBehaviour.Initialize(TimeSpan.FromSeconds(2), UnityEngine.Object.DestroyImmediate, meshRenderers);
                UISoundController.Instance.Play(UISoundType.GlobalMapLocationsSelect, pingObject);
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

        private NetworkPing GetPingedGuiElement()
        {
            return null;
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

        private TargetWrapper CreateTargetWrapper(NetworkTargetWrapper networkTargetWrapper)
        {
            if (networkTargetWrapper == null)
            {
                return null;
            }

            var point = new Vector3(networkTargetWrapper.Point.X, networkTargetWrapper.Point.Y, networkTargetWrapper.Point.Z);
            var unit = _gameStateLookupService.GetUnitEntity(networkTargetWrapper.UnitUniqueId);
            var wrapper = new TargetWrapper(point, networkTargetWrapper.Orientation, unit);
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

        private List<NetworkUnit> GetUnitsInCombat()
        {
            var unitsInCombat = Game.Instance.State.Units.InCombat().ToList();

            switch (Game.Instance.CurrentlyLoadedArea.name)
            {
                case "Prologue_Caves_1":
                    var anevia = Game.Instance.State.Units.FirstOrDefault(u => u.CharacterName == "Anevia");
                    if (anevia != null)
                    {
                        // Anevia, constantly joins midfight
                        unitsInCombat.Add(anevia);
                    }
                    break;
                default:
                    break;
            }

            var units = new List<NetworkUnit>();

            foreach (var combatUnit in unitsInCombat)
            {
                var unit = new NetworkUnit
                {
                    Id = combatUnit.UniqueId,
                    Position = new NetworkVector3(combatUnit.Position.x, combatUnit.Position.y, combatUnit.Position.z),
                    Orientation = combatUnit.Orientation,
                    TurnBasedInfo = GetUnitTurnBasedInfo(combatUnit),
                    CombatState = GetUnitCombatState(combatUnit),
                };
                units.Add(unit);
            }

            return units;
        }

        private NetworkUnitTurnBasedInfo GetUnitTurnBasedInfo(UnitEntityData combatUnit)
        {
            var unitInfo = Game.Instance.TurnBasedCombatController.FindUnitInfo(combatUnit);
            if (unitInfo == null)
            {
                return null;
            }

            var info = new NetworkUnitTurnBasedInfo
            {
                ActingInSurpriseRound = unitInfo.ActingInSurpriseRound,
                Surprising = unitInfo.Surprising,
                Surprised = unitInfo.Surprised,
            };

            return info;
        }

        private NetworkUnitCombatState GetUnitCombatState(UnitEntityData combatUnit)
        {
            if (combatUnit.CombatState == null)
            {
                return null;
            }

            var state = new NetworkUnitCombatState
            {
                EngagedUnits = [.. combatUnit.CombatState.EngagedUnits.Select(x => x.UniqueId)],
                EngagedBy = [.. combatUnit.CombatState.EngagedBy.Select(x => x.UniqueId)],
            };

            return state;
        }

        private void UpdateCombatState(NetworkCombatState networkCombatState, bool requiresFullUpdate)
        {
            try
            {
                var unitsToUpdate = networkCombatState.Units.ToDictionary(x => x, x => _gameStateLookupService.GetUnitEntity(x.Id));
                foreach (var (networkUnit, unit) in unitsToUpdate)
                {
                    if (unit == null)
                    {
                        _logger.LogError("Unable to update combat state for missing unit. UnitId={UnitId}", networkUnit.Id);
                        continue;
                    }

                    UpdateUnitPosition(unit, networkUnit);

                    if (requiresFullUpdate)
                    {
                        UpdateUnitTurnBasedInfo(unit, networkUnit.TurnBasedInfo);
                    }
                }

                UpdateCombatUnitState(unitsToUpdate);

                _logger.LogInformation("Finished updating combat state. RoundNumber={RoundNumber}, UnitsCount={UnitsCount}, IsFullUpdate={IsFullUpdate}", networkCombatState.RoundNumber, networkCombatState.Units.Count, requiresFullUpdate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to update combat state. RoundNumber={RoundNumber}, UnitsCount={UnitsCount}, IsFullUpdate={IsFullUpdate}", networkCombatState.RoundNumber, networkCombatState.Units.Count, requiresFullUpdate);
                throw;
            }
        }

        private void UpdateUnitTurnBasedInfo(UnitEntityData unit, NetworkUnitTurnBasedInfo networkUnitTurnBasedInfo)
        {
            var turnBasedInfo = Game.Instance.TurnBasedCombatController.FindUnitInfo(unit);

            if (turnBasedInfo == null || networkUnitTurnBasedInfo == null)
            {
                _logger.LogWarning("Unable to update missing turn based combat info. UnitId={UnitId}", unit.UniqueId);
                return;
            }

            turnBasedInfo.Surprising = networkUnitTurnBasedInfo.Surprising;
            turnBasedInfo.Surprised = networkUnitTurnBasedInfo.Surprised;
            turnBasedInfo.ActingInSurpriseRound = networkUnitTurnBasedInfo.ActingInSurpriseRound;
        }

        private void UpdateCombatUnitState(Dictionary<NetworkUnit, UnitEntityData> unitsToUpdate)
        {
            // engagement is configured for units pair so we need to clear existing lists before syncing
            foreach (var (_, unit) in unitsToUpdate)
            {
                if (unit?.CombatState == null)
                {
                    continue;
                }

                unit.CombatState.m_EngagedBy.Clear();
                unit.CombatState.m_EngagedUnits.Clear();
            }

            foreach (var (networkUnit, unit) in unitsToUpdate)
            {
                if (unit?.CombatState == null || networkUnit.CombatState == null)
                {
                    _logger.LogInformation("Unable to update missing combat unit state. UnitId={UnitId}", networkUnit.Id);
                    continue;
                }

                foreach (var engageTargetId in networkUnit.CombatState.EngagedUnits)
                {
                    var engageTarget = _gameStateLookupService.GetUnitEntity(engageTargetId);
                    if (engageTarget == null)
                    {
                        _logger.LogInformation("Unable to engage missing unit. UnitId={UnitId}, EngageTargetId={EngageTargetId}", unit.UniqueId, engageTargetId);
                        continue;
                    }

                    unit.CombatState.Engage(engageTarget);
                }
            }
        }

        private void UpdateUnitPosition(UnitEntityData unit, NetworkUnit networkUnit)
        {
            if (!unit.IsInCombat)
            {
                _logger.LogWarning("Updating unit outside of the combat. UnitId={UnitId}", networkUnit.Id);
            }

            if (unit.Orientation != networkUnit.Orientation)
            {
                var previousOrientation = unit.Orientation;
                _logger.LogInformation("Orientation has been updated. UnitId={UnitId}, PreviousOrientation={PreviousOrientation}, NewOrientation={NewOrientation}", unit.UniqueId, previousOrientation.ToString("F4"), unit.Orientation.ToString("F4"));
                unit.Orientation = networkUnit.Orientation;
            }

            if (unit.Position.x != networkUnit.Position.X
                || unit.Position.y != networkUnit.Position.Y
                || unit.Position.z != networkUnit.Position.Z)
            {
                var newPosition = new Vector3(networkUnit.Position.X, networkUnit.Position.Y, networkUnit.Position.Z);
                _logger.LogInformation("Updating unit position. UnitId={UnitId}, PreviousPosition={PreviousPosition}, NewPosition={NewPosition}", unit.UniqueId, unit.Position.ToString("F4"), newPosition.ToString("F4"));
                unit.Translocate(newPosition, unit.Orientation);
            }
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
                        : GetNeareastLootableMapObjects(lootableEntity.Position);

                    var mapObjectContainers = lookupTargets.Select(x => ((InteractionLootPart)x.Interactions.FindOrDefault(i => i is InteractionLootPart)).Loot);
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
                var itemSlot = unit.Body.QuickSlots.FirstOrDefault(s => IsSameItem(s.Item, networkActionBarSlot.Item));
                if (itemSlot == null)
                {
                    _logger.LogError("Unable to find item slot content. UnitId={UnitId}, ItemId={ItemId}, ItemName={ItemName}", unit.UniqueId, networkActionBarSlot.Item.UniqueId, networkActionBarSlot.Item.Name);
                    return null;
                }

                var itemActionBarSlot = new MechanicActionBarSlotItem { Item = itemSlot.Item, Unit = unit };
                return itemActionBarSlot;
            }

            var ability = FindAbility(unit, networkActionBarSlot.Ability);
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

        private SpellSlot GetSpellSlot(Spellbook spellbook, NetworkSpellSlot slot)
        {
            if (spellbook.m_MemorizedSpells.Length < slot.SpellLevel)
            {
                return null;
            }

            var spellLevel = spellbook.m_MemorizedSpells[slot.SpellLevel];
            var spellSlot = spellLevel.FirstOrDefault(s => s.Index == slot.Index && s.Type == slot.Type);

            return spellSlot;
        }

        private void RefreshSpellbookUI()
        {
            _uiAccessor.SpellbookMemorizingVM?.UpdateSlots();
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
                (string.IsNullOrEmpty(transfer.Item.HoldingSlotOwnerId) || string.Equals(i.HoldingSlot?.Owner?.Unit.UniqueId, transfer.Item.HoldingSlotOwnerId, StringComparison.OrdinalIgnoreCase)))
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

        private MapObjectEntityData GetNeareastLootBagMapObject(NetworkVector3 position)
        {
            var allNearest = GetNeareastLootableMapObjects(position);
            var lootbag = allNearest.FirstOrDefault(o => o is DroppedLoot.EntityData);
            _logger.LogInformation("Using nearest lootbag as a map object. MapObjectId={MapObjectId}, Position={Position}", lootbag?.UniqueId, lootbag?.Position);
            return lootbag;
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
                && itemEntity.Enchantments.FirstOrDefault()?.NameForAcronym == networkLootItem.FirstEnchantmentName
                && itemEntity.Enchantments.Count == networkLootItem.EnchantmentsCount;

            return sameItemType;
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

        private List<MapObjectEntityData> GetNeareastLootableMapObjects(NetworkVector3 position)
        {
            var targetPoint = new Vector3(position.X, position.Y, position.Z);
            var orderedContainers = Game.Instance.State.MapObjects.All
                .Where(o => o.Interactions.Any(i => i is InteractionLootPart))
                .OrderBy(o => (o.Position - targetPoint).magnitude)
                .ToList();

            return orderedContainers;
        }

        private ActivatableAbility FindActivatableAbility(UnitEntityData caster, NetworkActivatableAbility activatableAbility)
        {
            var abilities = caster.ActivatableAbilities?.Enumerable ?? [];
            var ability = abilities.FirstOrDefault(a => string.Equals(a.UniqueId, activatableAbility.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(a.NameForAcronym, activatableAbility.Name, StringComparison.OrdinalIgnoreCase));

            return ability;
        }

        private AbilityData GetKnownSpell(Spellbook spellbook, string abilityId, string abilityName)
        {
            for (int level = 0; level < spellbook.m_KnownSpells.Length; level++)
            {
                var spellLevel = spellbook.m_KnownSpells[level];
                var spellSlot = spellLevel.FirstOrDefault(s => string.Equals(s.UniqueId, abilityId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.NameForAcronym, abilityName, StringComparison.OrdinalIgnoreCase));

                if (spellSlot != null)
                {
                    return spellSlot;
                }
            }

            return null;
        }

        private AbilityData GetMemorizedSpell(Spellbook spellbook, string abilityId, string abilityName)
        {
            for (int level = 0; level < spellbook.m_MemorizedSpells.Length; level++)
            {
                var spellLevel = spellbook.m_MemorizedSpells[level];
                var spellSlot = spellLevel.FirstOrDefault(s => string.Equals(s.Spell?.UniqueId, abilityId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.Spell?.NameForAcronym, abilityName, StringComparison.OrdinalIgnoreCase));

                if (spellSlot != null)
                {
                    return spellSlot.Spell;
                }
            }

            return null;
        }

        private AbilityData FindAbilityInSpellbook(UnitEntityData unit, NetworkAbility networkAbility)
        {
            var spellbook = unit.Spellbooks.FirstOrDefault(s => string.Equals(s.Blueprint.Name.Key, networkAbility.SpellbookId));
            if (spellbook == null)
            {
                _logger.LogError("Unable to 4find ability due to missing spellbook. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookId={SpellbookId}", unit.UniqueId, networkAbility.Id, networkAbility.SpellbookId);
                return null;
            }

            if (!string.IsNullOrEmpty(networkAbility.ConvertedFromId))
            {
                var spellConversionSource = GetKnownSpell(spellbook, networkAbility.ConvertedFromId, networkAbility.Name) ?? GetMemorizedSpell(spellbook, networkAbility.ConvertedFromId, networkAbility.Name);
                if (spellConversionSource == null)
                {
                    _logger.LogError("Can't find spell conversion source for converted ability. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}, ConvertedAbilityId={ConvertedAbilityId}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name, networkAbility.ConvertedFromId);
                    return null;
                }

                var convertedSpell = GetConvertedAbility(spellConversionSource, networkAbility);
                if (convertedSpell == null)
                {
                    _logger.LogError("Can't find target ability in spell conversion list. UnitId={UnitId}, AbilityId={abilityId}, SpellbookName={SpellbookName}, ConvertedAbilityId={ConvertedAbilityId}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name, networkAbility.ConvertedFromId);
                    return null;
                }

                _logger.LogInformation("Converted spell has been found. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return convertedSpell;
            }

            var knownSpell = GetKnownSpell(spellbook, networkAbility.Id, networkAbility.Name);
            if (knownSpell != null)
            {
                _logger.LogInformation("Spell has been found in known spells. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return knownSpell;
            }

            var memorizedSpell = GetMemorizedSpell(spellbook, networkAbility.Id, networkAbility.Name);
            if (memorizedSpell != null)
            {
                _logger.LogInformation("Spell has been found in memorized spells. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookName={SpellbookName}", unit.UniqueId, networkAbility.Id, spellbook.Blueprint.Name);
                return memorizedSpell;
            }

            return null;
        }

        private AbilityData GetConvertedAbility(AbilityData conversionSource, NetworkAbility networkAbility)
        {
            var convertedSpell = conversionSource.GetConversions().FirstOrDefault(
                    c => string.Equals(c.UniqueId, networkAbility.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.NameForAcronym, networkAbility.Name, StringComparison.OrdinalIgnoreCase));

            return convertedSpell;
        }

        /// <summary>
        /// I never thought it would be so rough to find casted ability
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="abilityUse"></param>
        /// <returns></returns>
        private AbilityData FindAbility(UnitEntityData unit, NetworkAbility abilityUse)
        {
            if (!string.IsNullOrEmpty(abilityUse.SpellbookId))
            {
                return FindAbilityInSpellbook(unit, abilityUse);
            }

            if (!string.IsNullOrEmpty(abilityUse.ConvertedFromId))
            {
                var conversionAbility = unit.Abilities.Enumerable.FirstOrDefault(a => string.Equals(a.Data.UniqueId, abilityUse.ConvertedFromId, StringComparison.OrdinalIgnoreCase));
                if (conversionAbility == null)
                {
                    _logger.LogInformation("Unable to find ability for conversion. UnitId={UnitId}, AbilityId={AbilityId}", unit.UniqueId, abilityUse.ConvertedFromId);
                    return null;
                }
                var convertedAbility = GetConvertedAbility(conversionAbility.Data, abilityUse);
                if (convertedAbility == null)
                {
                    _logger.LogInformation("Unable to find ability in conversion list. UnitId={UnitId}, AbilityId={AbilityId}", unit.UniqueId, abilityUse.ConvertedFromId);
                }

                return convertedAbility;
            }

            var byAbilityId = unit.Abilities.Enumerable.FirstOrDefault(a => string.Equals(a.Data.UniqueId, abilityUse.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a.Data.NameForAcronym, abilityUse.Name, StringComparison.OrdinalIgnoreCase));

            if (byAbilityId != null)
            {
                _logger.LogInformation("Ability has been found by abilityId. UnitId={UnitId}, AbilityId={AbilityId}", unit.UniqueId, abilityUse.Id);
                return byAbilityId.Data;
            }

            return null;
        }

        private void ExecuteClickHandler(IClickEventHandler clickEventHandler, NetworkClick click)
        {
            var targetUnit = _gameStateLookupService.GetUnitEntity(click.TargetUnitId);
            var selectedUnits = click.SelectedUnits.Select(_gameStateLookupService.GetUnitEntity).ToList();
            var selectedUnit = selectedUnits.FirstOrDefault();
            var worldPosition = new Vector3(click.WorldPosition.X, click.WorldPosition.Y, click.WorldPosition.Z);

            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    _logger.LogInformation("Executing click handler. HandlerType={HandlerType}, WorldPosition={WorldPosition}, TargetUnitId={TargetUnitId}, SelectedUnit={SelectedUnit}",
                               clickEventHandler.GetType().Name, click.WorldPosition, targetUnit?.UniqueId, selectedUnit?.UniqueId);

                    using var context = _networkExecutionContext.Value = RemoteExecutionContext.Create(selectedUnits);
                    Game.Instance.SelectionCharacter.SelectedUnit.Value = selectedUnit;

                    if (!string.IsNullOrEmpty(click.MovementLimit) && Game.Instance.TurnBasedCombatController.CurrentTurn != null)
                    {
                        Enum.TryParse<TurnBased.Controllers.TurnController.MovementLimit>(click.MovementLimit, true, out var limit);
                        Game.Instance.TurnBasedCombatController.CurrentTurn.SetMovementLimit(limit);
                        _logger.LogInformation("Movement limit has been updated. UnitId={UnitId}, Limit={Limit}", Game.Instance.TurnBasedCombatController.CurrentTurn.Rider.UniqueId, limit);
                    }

                    if (click.VectorPath != null && click.VectorPath.Count > 0)
                    {
                        var movementPath = click.VectorPath.Select(v => new Vector3(v.X, v.Y, v.Z)).ToList();
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
