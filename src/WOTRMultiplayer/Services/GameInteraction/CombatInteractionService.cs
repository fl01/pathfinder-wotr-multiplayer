using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kingmaker;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.Armies.TacticalCombat.Blueprints;
using Kingmaker.Armies.TacticalCombat.Commands;
using Kingmaker.Armies.TacticalCombat.Controllers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.Pathfinding;
using Kingmaker.PubSubSystem;
using Kingmaker.TurnBasedMode;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using UniRx;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Units;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class CombatInteractionService : ICombatInteractionService
    {
        private readonly ILogger<CombatInteractionService> _logger;
        private readonly IGameStateLookupService _gameStateLookupService;
        private readonly IMainThreadAccessor _mainThreadAccessor;

        public CombatInteractionService(
            ILogger<CombatInteractionService> logger,
            IGameStateLookupService gameStateLookupService,
            IMainThreadAccessor mainThreadAccessor)
        {
            _logger = logger;
            _gameStateLookupService = gameStateLookupService;
            _mainThreadAccessor = mainThreadAccessor;
        }

        public void UpdateIsInCombatStatus()
        {
            Game.Instance.Player.UpdateIsInCombat();
        }

        public bool IsInCombat()
        {
            return Game.Instance.Player.IsInCombat;
        }

        public bool IsInCrusadeTacticalCombat()
        {
            return TacticalCombatHelper.IsActive;
        }

        public int GetCrusadeArmyCombatAreaSeed()
        {
            var areaSeed = Game.Instance.TacticalCombat?.Data?.Seed ?? -1;
            return areaSeed;
        }

        public void InitializeCrusadeArmyCombat()
        {
            _mainThreadAccessor.Post(() =>
            {
                var gameMode = Game.Instance.m_GameModes.Peek();
                if (gameMode.Type != GameModeType.TacticalCombat && Game.Instance.CurrentlyLoadedArea is not BlueprintTacticalCombatArea)
                {
                    _logger.LogError("Unable to initialize crusade army combat due to invalid area/game mode. GameModeType={Type}, AreaType={AreaType}", gameMode.Type.Name, Game.Instance.CurrentlyLoadedArea?.GetType().Name);
                    return;
                }

                var intialziationController = gameMode.GetController<TacticalCombatInitializationController>();
                if (intialziationController == null)
                {
                    _logger.LogError("Unable to initialize crusade army combat due to missing TacticalCombatInitializationController");
                    return;
                }

                intialziationController.Activate();
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

        public bool IsCombatTurnFinished()
        {
            var turnStatus = Game.Instance.TurnBasedCombatController.CurrentTurn?.Status ?? TurnBased.Controllers.TurnController.TurnStatus.None;
            return turnStatus == TurnBased.Controllers.TurnController.TurnStatus.None
                || turnStatus == TurnBased.Controllers.TurnController.TurnStatus.Ended
                || turnStatus == TurnBased.Controllers.TurnController.TurnStatus.Ending;
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

        public void RunTacticalUnitAttackCommand(NetworkTacticalUnitAttackCommand tacticalUnitAttackCommand)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (!TacticalCombatHelper.IsActive)
                {
                    _logger.LogWarning("Unable to run {command} due to inactive tactical combat. UnitId={UnitId}", nameof(NetworkTacticalUnitAttackCommand), tacticalUnitAttackCommand.UnitId);
                    return;
                }

                var unit = _gameStateLookupService.GetUnitEntity(tacticalUnitAttackCommand.UnitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to run {command} due to missing unit. UnitId={UnitId}", nameof(NetworkTacticalUnitAttackCommand), tacticalUnitAttackCommand.UnitId);
                    return;
                }

                var targetUnit = _gameStateLookupService.GetUnitEntity(tacticalUnitAttackCommand.TargetUnitId);
                if (targetUnit == null)
                {
                    _logger.LogError("Unable to run {command} due to missing target unit. UnitId={UnitId}, TargetUnitId={TargetUnitId}", nameof(NetworkTacticalUnitAttackCommand), tacticalUnitAttackCommand.UnitId, tacticalUnitAttackCommand.TargetUnitId);
                    return;
                }

                var attackCommand = new TacticalCombatUnitAttack(targetUnit)
                {
                    ForcedPath = CreateForcedPath(tacticalUnitAttackCommand.Path),
                    ShouldSkipAnimation = Game.Instance.TacticalCombat.Data.Accelerated
                };
                unit.Commands.Run(attackCommand);
                _logger.LogInformation("Command {command} has been executed. UnitId={UnitId}", nameof(NetworkTacticalUnitAttackCommand), tacticalUnitAttackCommand.UnitId);
            });
        }

        public void RunTacticalUnitUseAbilityCommand(NetworkTacticalUnitUseAbilityCommand tacticalUnitUseAbilityCommand)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (!TacticalCombatHelper.IsActive)
                {
                    _logger.LogWarning("Unable to run {command} due to inactive tactical combat. UnitId={UnitId}", nameof(NetworkTacticalUnitUseAbilityCommand), tacticalUnitUseAbilityCommand.Ability.CasterId);
                    return;
                }

                var unit = _gameStateLookupService.GetUnitEntity(tacticalUnitUseAbilityCommand.Ability.CasterId);
                if (unit == null)
                {
                    _logger.LogError("Unable to run {command} due to missing unit. UnitId={UnitId}", nameof(NetworkTacticalUnitUseAbilityCommand), tacticalUnitUseAbilityCommand.Ability.CasterId);
                    return;
                }

                var ability = _gameStateLookupService.FindAbility(unit, tacticalUnitUseAbilityCommand.Ability);
                if (ability == null)
                {
                    _logger.LogError("Unable to run {command} due to missing ability. UnitId={UnitId}, AbilityId={AbilityId}", nameof(NetworkTacticalUnitUseAbilityCommand), tacticalUnitUseAbilityCommand.Ability.CasterId, tacticalUnitUseAbilityCommand.Ability.Id);
                    return;
                }

                var target = _gameStateLookupService.GetUnitEntity(tacticalUnitUseAbilityCommand.Ability.Target.UnitId);
                var point = tacticalUnitUseAbilityCommand.Ability.Target.Point.ToUnityVector3(); ;
                var targetWrapper = new TargetWrapper(point, null, target);
                var useAbilityCommand = new TacticalCombatUnitUseAbility(ability, targetWrapper)
                {
                    ForcedPath = CreateForcedPath(tacticalUnitUseAbilityCommand.Ability.VectorPath),
                    ShouldSkipAnimation = Game.Instance.TacticalCombat.Data.Accelerated
                };

                EventBus.RaiseEvent<IClickActionHandler>(x => x.OnCastRequested(ability, target), true);
                unit.Commands.Run(useAbilityCommand);
                _logger.LogInformation("Command {command} has been executed. UnitId={UnitId}, AbilityId={AbilityId}, AbilityName={AbilityName}", nameof(NetworkTacticalUnitUseAbilityCommand), tacticalUnitUseAbilityCommand.Ability.CasterId, tacticalUnitUseAbilityCommand.Ability.Id, tacticalUnitUseAbilityCommand.Ability.Name);
            });
        }

        public void RunTacticalUnitMoveToCommand(NetworkTacticalUnitMoveToCommand tacticalUnitMoveToCommand)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (!TacticalCombatHelper.IsActive)
                {
                    _logger.LogWarning("Unable to run {command} due to inactive tactical combat. UnitId={UnitId}", nameof(NetworkTacticalUnitMoveToCommand), tacticalUnitMoveToCommand.UnitId);
                    return;
                }

                var unit = _gameStateLookupService.GetUnitEntity(tacticalUnitMoveToCommand.UnitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to run {command} due to missing unit. UnitId={UnitId}", nameof(NetworkTacticalUnitMoveToCommand), tacticalUnitMoveToCommand.UnitId);
                    return;
                }

                var forcedPath = CreateForcedPath(tacticalUnitMoveToCommand.Path);
                var moveToCommand = new UnitMoveTo(forcedPath.vectorPath.Last())
                {
                    ForcedPath = forcedPath,
                };

                unit.Commands.Run(moveToCommand);
                _logger.LogInformation("Command {command} has been executed. UnitId={UnitId}", nameof(NetworkTacticalUnitMoveToCommand), tacticalUnitMoveToCommand.UnitId);
            });
        }

        public void UseTacticalCombatTotalDefense()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (!TacticalCombatHelper.IsActive)
                {
                    _logger.LogWarning("Unable to use total defense due to inactive tactical combat");
                    return;
                }

                Game.Instance.TacticalCombat.TurnController.UseTotalDefense(true);
                _logger.LogInformation("Total defense has been used");
            });
        }

        public void PostponeTacticalCombatTurn()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (!TacticalCombatHelper.IsActive)
                {
                    _logger.LogWarning("Unable to postpone turn due to inactive tactical combat");
                    return;
                }

                Game.Instance.TacticalCombat.TurnController.PostponeTurn(true);
                _logger.LogInformation("Combat turn has been postponed");
            });
        }

        public void RetreatFromTacticalCombat()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (!TacticalCombatHelper.IsActive)
                {
                    _logger.LogWarning("Unable to retreat due to inactive tactical combat");
                    return;
                }

                Game.Instance.TacticalCombat.RetreatFromBattle();
                _logger.LogInformation("Retreated from tactical combat");
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

                    RunUnitAttackCommand(executor, attack);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to attack unit. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}", attack.ExecutorUnitId, attack.TargetUnitId);
                    throw;
                }
            });
        }

        public void UseAbility(NetworkAbility networkAbility)
        {
            try
            {
                _mainThreadAccessor.Post(() =>
                {
                    var executor = _gameStateLookupService.GetUnitEntity(networkAbility.CasterId);
                    if (executor == null)
                    {
                        _logger.LogError("Unable to find executor unit to perform unit ability use. ExecutorUnitId={ExecutorUnitId}", networkAbility.CasterId);
                        return;
                    }


                    if (!Enum.TryParse<UnitCommand.CommandType>(networkAbility.CommandType, true, out var commandType))
                    {
                        _logger.LogWarning("Unable to parse command type. Type={Type}", networkAbility.CommandType);
                        commandType = UnitCommand.CommandType.Standard;
                    }

                    RunUnitAbilityCommand(executor, networkAbility, commandType);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiate UseAbility. CasterId={CasterId}, TargetUnitId={TargetUnitId}, AbilityId={AbilityId}", networkAbility.CasterId, networkAbility.Target.UnitId, networkAbility.Id);
                throw;
            }
        }

        private void SetTurnMovementLimit(string rawMovementLimit, UnitEntityData executor)
        {
            var turn = Game.Instance.TurnBasedCombatController.CurrentTurn;
            if (!string.IsNullOrEmpty(rawMovementLimit) && turn != null)
            {
                Enum.TryParse<TurnBased.Controllers.TurnController.MovementLimit>(rawMovementLimit, true, out var movementLimit);
                turn.CurrentMovementLimit = movementLimit;
                _logger.LogInformation("Unit movement limit has been updated. ExecutorUnitId={ExecutorUnitId}, Limit={Limit}", executor.UniqueId, movementLimit);
            }
        }

        private static ForcedPath CreateForcedPath(List<NetworkVector3> path)
        {
            if (path == null || path.Count == 0)
            {
                return null;
            }

            var vectorPath = path.Select(v => v.ToUnityVector3()).ToList();
            var forcedPath = new ForcedPath(vectorPath);
            return forcedPath;
        }

        private List<NetworkUnit> GetUnitsInCombat()
        {
            var unitsInCombat = Game.Instance.State.Units.InCombat().ToList();

            switch (Game.Instance.CurrentlyLoadedArea.name)
            {
                case "Prologue_Caves_1":
                    var anevia = Game.Instance.State.Units.FirstOrDefault(u => string.Equals(u.CharacterName, "Anevia", StringComparison.OrdinalIgnoreCase));
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
                    Position = combatUnit.Position.ToNetworkVector3(),
                    Orientation = combatUnit.Orientation,
                    TurnBasedInfo = GetUnitTurnBasedInfo(combatUnit),
                    CombatState = GetUnitCombatState(combatUnit),
                    CurrentAbility = GetUnitAbilityCommand(combatUnit),
                    CurrentAttack = GetUnitAttackCommand(combatUnit),
                };

                units.Add(unit);
            }

            return units;
        }

        private NetworkUnitAttack GetUnitAttackCommand(UnitEntityData combatUnit)
        {
            var attackCommand = combatUnit.Commands.Attack;
            if (attackCommand == null)
            {
                return null;
            }

            var path = PathVisualizer.Instance?.CurrentPathForUnit(attackCommand.Executor.View);
            var networkPath = path?.vectorPath.Select(v => v.ToNetworkVector3()).ToList();
            var attack = new NetworkUnitAttack
            {
                ExecutorUnitId = attackCommand.Executor.UniqueId,
                TargetUnitId = attackCommand.TargetUnit?.UniqueId,
                IsFullAttack = attackCommand.IsAttackFull,
                IsSingleAttack = attackCommand.IsSingleAttack,
                VectorPath = networkPath
            };

            return attack;
        }

        private NetworkAbility GetUnitAbilityCommand(UnitEntityData combatUnit)
        {
            var useAbilityCommand = combatUnit.Commands.UnitUseAbility;
            if (useAbilityCommand == null)
            {
                return null;
            }

            var path = PathVisualizer.Instance?.CurrentPathForUnit(useAbilityCommand.Executor.View);
            var networkPath = path?.vectorPath.Select(v => v.ToNetworkVector3()).ToList();
            var ability = new NetworkAbility
            {
                Id = useAbilityCommand.Ability.UniqueId,
                Name = useAbilityCommand.Ability.NameForAcronym,
                SpellbookId = useAbilityCommand.Ability.Spellbook?.Blueprint.Name.Key,
                CasterId = useAbilityCommand.Executor.UniqueId,
                VectorPath = networkPath,
                Target = new NetworkTargetWrapper(
                    useAbilityCommand.Target.Point.ToNetworkVector3(),
                    useAbilityCommand.Target.Orientation,
                    useAbilityCommand.Target.Unit?.UniqueId),
                CommandType = useAbilityCommand.Type.ToString(),
                ConvertedFromId = useAbilityCommand.Ability.ConvertedFrom?.UniqueId
            };

            return ability;
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
                        OverrideUnitOffensiveCommands(unit, networkUnit);
                        UpdateUnitTurnBasedInfo(unit, networkUnit.TurnBasedInfo);
                    }
                }

                UpdateCombatUnitState(unitsToUpdate);

                _logger.LogInformation("Combat state has been updated. RoundNumber={RoundNumber}, UnitsCount={UnitsCount}, IsFullUpdate={IsFullUpdate}", networkCombatState.RoundNumber, networkCombatState.Units.Count, requiresFullUpdate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to update combat state. RoundNumber={RoundNumber}, UnitsCount={UnitsCount}, IsFullUpdate={IsFullUpdate}", networkCombatState.RoundNumber, networkCombatState.Units.Count, requiresFullUpdate);
                throw;
            }
        }

        private void OverrideUnitOffensiveCommands(UnitEntityData unit, NetworkUnit networkUnit)
        {
            unit.Commands.InterruptAll(raiseEvent: true);

            if (networkUnit.CurrentAttack != null)
            {
                RunUnitAttackCommand(unit, networkUnit.CurrentAttack);
            }
            else if (networkUnit.CurrentAbility != null)
            {
                RunUnitAbilityCommand(unit, networkUnit.CurrentAbility);
            }
        }

        private void RunUnitAttackCommand(UnitEntityData executorUnit, NetworkUnitAttack networkUnitAttack)
        {
            var target = _gameStateLookupService.GetUnitEntity(networkUnitAttack.TargetUnitId);
            if (target == null)
            {
                _logger.LogError("Unable to run unit attack command due to missing target unit. TargetUnitId={TargetUnitId}", networkUnitAttack.TargetUnitId);
                return;
            }

            var command = UnitAttack.CreateAttackCommand(executorUnit, target) as UnitAttack;
            command.ForceFullAttack = !networkUnitAttack.IsSingleAttack && networkUnitAttack.IsFullAttack;
            command.IsSingleAttack = networkUnitAttack.IsSingleAttack;

            var turn = Game.Instance.TurnBasedCombatController.CurrentTurn;
            if (turn != null)
            {
                turn.m_AttackMode = command.IsSingleAttack ? TurnBased.Controllers.TurnController.AttackMode.SingleAttack : TurnBased.Controllers.TurnController.AttackMode.FullAttack;
            }

            SetCommandPath(networkUnitAttack.VectorPath, command);
            SetTurnMovementLimit(networkUnitAttack.MovementLimit, executorUnit);

            _logger.LogInformation("Unit Attack command has been initiated. UnitId={UnitId}, TargetUnitId={TargetUnitId}, ForceFullAttack={ForceFullAttack}, Path={Path}, AttackMode={AttackMode}, MovementLimit={MovementLimit}",
                executorUnit.UniqueId, networkUnitAttack.TargetUnitId, command.ForceFullAttack, command.ForcedPath?.vectorPath, turn?.m_AttackMode, turn?.CurrentMovementLimit);
            executorUnit.Commands.Run(command);
        }

        private void SetCommandPath(List<NetworkVector3> path, UnitCommand command)
        {
            if (path == null)
            {
                return;
            }

            var movementPath = path.Select(v => v.ToUnityVector3()).ToList();
            command.ForcedPath = new ForcedPath(movementPath);
            if (PathVisualizer.Instance != null)
            {
                PathVisualizer.Instance.m_CurrentPath = command.ForcedPath;
                PathVisualizer.Instance.m_CurrentPath.Claim(PathVisualizer.Instance);
            }
        }

        private void RunUnitAbilityCommand(UnitEntityData executorUnit, NetworkAbility networkAbility, UnitCommand.CommandType? commandType = null)
        {
            var abilityData = _gameStateLookupService.FindAbility(executorUnit, networkAbility);
            if (abilityData == null)
            {
                _logger.LogError("Unable to run unit ability command due to missing ability. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookBlueprintId={SpellbookBlueprintId}", executorUnit.UniqueId, networkAbility.Id, networkAbility.SpellbookId);
                return;
            }

            var target = CreateTargetWrapper(networkAbility.Target);
            var command = UnitUseAbility.CreateCastCommand(abilityData, target, commandType ?? abilityData.RuntimeActionType);
            command.CreatedByPlayer = true;
            SetCommandPath(networkAbility.VectorPath, command);
            SetTurnMovementLimit(networkAbility.MovementLimit, executorUnit);

            _logger.LogInformation("Unit UseAbility command has been initiated. UnitId={UnitId}, TargetUnitId={TargetUnitId}, TargetPoint={TargetPoint}, AbilityId={AbilityId}, AbilityName={AbilityName}", executorUnit.UniqueId, networkAbility.Target.Point, networkAbility.Target.UnitId, networkAbility.Id, networkAbility.Name);
            executorUnit.Commands.Run(command);
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

        private TargetWrapper CreateTargetWrapper(NetworkTargetWrapper networkTargetWrapper)
        {
            if (networkTargetWrapper == null)
            {
                return null;
            }

            var point = networkTargetWrapper.Point.ToUnityVector3();
            var unit = _gameStateLookupService.GetUnitEntity(networkTargetWrapper.UnitId);
            var wrapper = new TargetWrapper(point, networkTargetWrapper.Orientation, unit);
            return wrapper;
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
                unit.CombatState.DisengageAttackTargets.Clear();
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
                var newPosition = networkUnit.Position.ToUnityVector3();
                _logger.LogInformation("Updating unit position. UnitId={UnitId}, PreviousPosition={PreviousPosition}, NewPosition={NewPosition}", unit.UniqueId, unit.Position.ToString("F4"), newPosition.ToString("F4"));
                unit.Translocate(newPosition, unit.Orientation);
            }
        }
    }
}
