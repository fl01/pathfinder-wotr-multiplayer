using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using Kingmaker;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.Armies.TacticalCombat.Blueprints;
using Kingmaker.Armies.TacticalCombat.Commands;
using Kingmaker.Armies.TacticalCombat.Controllers;
using Kingmaker.Controllers.Combat;
using Kingmaker.Designers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Pathfinding;
using Kingmaker.PubSubSystem;
using Kingmaker.TurnBasedMode;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Class.Kineticist;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using TurnBased.Controllers;
using UniRx;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.GameInteraction.CombatLog;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.AreaEffects;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Units;
using WOTRMultiplayer.Entities.Units.Parts;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class CombatInteractionService : ICombatInteractionService
    {
        private readonly ILogger<CombatInteractionService> _logger;
        private readonly IGameStateLookupService _gameStateLookupService;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IPlayerNotificationService _playerNotificationService;
        private readonly IBuffInteractionService _buffInteractionService;
        private readonly IMapper _mapper;

        public CombatInteractionService(
            ILogger<CombatInteractionService> logger,
            IGameStateLookupService gameStateLookupService,
            IPlayerNotificationService playerNotificationService,
            IBuffInteractionService buffInteractionService,
            IMainThreadAccessor mainThreadAccessor,
            IMapper mapper)
        {
            _logger = logger;
            _gameStateLookupService = gameStateLookupService;
            _mainThreadAccessor = mainThreadAccessor;
            _playerNotificationService = playerNotificationService;
            _buffInteractionService = buffInteractionService;
            _mapper = mapper;
        }

        public void UpdateIsInCombatStatus()
        {
            Game.Instance.Player.UpdateIsInCombat();
        }

        public bool IsInCombat()
        {
            return Game.Instance.Player.IsInCombat;
        }

        public bool IsInCombat(string unitId)
        {
            var unit = _gameStateLookupService.GetUnitEntity(unitId);
            return unit != null && unit.IsInCombat;
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
                if (!TacticalCombatHelper.IsActive || Game.Instance.CurrentlyLoadedArea is not BlueprintTacticalCombatArea)
                {
                    _logger.LogError("Unable to initialize crusade army combat due to invalid area/game mode. GameModeType={Type}, AreaType={AreaType}", Game.Instance.CurrentlyLoadedArea?.GetType().Name);
                    return;
                }

                var gameMode = Game.Instance.m_GameModes.Peek();
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
            var turnStatus = Game.Instance.TurnBasedCombatController.CurrentTurn?.Status ?? TurnController.TurnStatus.None;
            return turnStatus == TurnController.TurnStatus.None
                || turnStatus == TurnController.TurnStatus.Ended
                || turnStatus == TurnController.TurnStatus.Ending;
        }

        public void StartTurnBasedCombatTurn(string unitId)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var currentUnit = _gameStateLookupService.GetUnitEntity(unitId);
                    if (currentUnit == null)
                    {
                        _logger.LogError("Unable to start turn based turn due to missing unit. UnitId={UnitId}", unitId);
                        return;
                    }

                    _logger.LogInformation("Starting turn based turn. UnitId={UnitId}", unitId);
                    Game.Instance.TurnBasedCombatController?.StartTurn(currentUnit);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to start CombatController.StartTurn");
                    throw;
                }
            });
        }

        public void EndTurnBasedCombatTurn(bool isAI)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var turnStatus = Game.Instance.TurnBasedCombatController.CurrentTurn?.Status ?? null;
                    if ((turnStatus == TurnController.TurnStatus.Ending && !isAI)
                        || turnStatus == TurnController.TurnStatus.Ended
                        || turnStatus == TurnController.TurnStatus.None)
                    {
                        _logger.LogWarning("Cannot end already finished turn. TurnStatus={TurnStatus}", turnStatus);
                        return;
                    }

                    _logger.LogInformation("Turn based turn has been ended. isAI={isAI}, TurnStatus={TurnStatus}", isAI, turnStatus);
                    Game.Instance.TurnBasedCombatController.CurrentTurn?.ToEnd();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while ending turn");
                    throw;
                }
            });
        }

        public NetworkCombatState GetCombatState()
        {
            var areaEffects = _gameStateLookupService.GetAreaEffects();
            var state = new NetworkCombatState
            {
                RoundNumber = Game.Instance.TurnBasedCombatController.RoundNumber,
                HasSurpriseRound = Game.Instance.TurnBasedCombatController.m_HasSurpriseRound,
                Units = GetUnitsInCombat(),
                AreaEffects = _mapper.Map<List<NetworkAreaEffect>>(areaEffects)
            };

            return state;
        }

        public Task UpdateCombatStateAsync(NetworkCombatState networkCombatState, List<NetworkAreaEffect> networkAreaEffects, bool requiresFullUpdate)
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

                    TriggerAreaEffects(networkAreaEffects);

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
                    _logger.LogWarning("Unable to run {command} due to inactive tactical combat. UnitId={UnitId}", nameof(NetworkTacticalUnitUseAbilityCommand), tacticalUnitUseAbilityCommand.InitiatorUnitId);
                    return;
                }

                var unit = _gameStateLookupService.GetUnitEntity(tacticalUnitUseAbilityCommand.InitiatorUnitId);
                if (unit == null)
                {
                    _logger.LogError("Unable to run {command} due to missing unit. UnitId={UnitId}", nameof(NetworkTacticalUnitUseAbilityCommand), tacticalUnitUseAbilityCommand.InitiatorUnitId);
                    return;
                }

                var ability = _gameStateLookupService.FindAbility(unit, tacticalUnitUseAbilityCommand.Ability);
                if (ability == null)
                {
                    _logger.LogError("Unable to run {command} due to missing ability. UnitId={UnitId}, AbilityId={AbilityId}", nameof(NetworkTacticalUnitUseAbilityCommand), tacticalUnitUseAbilityCommand.InitiatorUnitId, tacticalUnitUseAbilityCommand.Ability.Id);
                    return;
                }

                var target = _gameStateLookupService.GetUnitEntity(tacticalUnitUseAbilityCommand.Target.UnitId);
                var point = tacticalUnitUseAbilityCommand.Target.Point.ToUnityVector3();
                var targetWrapper = new TargetWrapper(point, null, target);
                var useAbilityCommand = new TacticalCombatUnitUseAbility(ability, targetWrapper)
                {
                    ForcedPath = CreateForcedPath(tacticalUnitUseAbilityCommand.VectorPath),
                    ShouldSkipAnimation = Game.Instance.TacticalCombat.Data.Accelerated
                };

                EventBus.RaiseEvent<IClickActionHandler>(x => x.OnCastRequested(ability, target));
                unit.Commands.Run(useAbilityCommand);
                _logger.LogInformation("Command {command} has been executed. UnitId={UnitId}, AbilityId={AbilityId}, AbilityName={AbilityName}", nameof(NetworkTacticalUnitUseAbilityCommand), tacticalUnitUseAbilityCommand.InitiatorUnitId, tacticalUnitUseAbilityCommand.Ability.Id, tacticalUnitUseAbilityCommand.Ability.Name);
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
        public void MoveUnit(NetworkUnitMoveTo unitMoveTo)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var executor = _gameStateLookupService.GetUnitEntity(unitMoveTo.InitiatorUnitId);
                    if (executor == null)
                    {
                        _logger.LogError("Unable to find executor unit to perform unit moveto command. InitiatorUnitId={InitiatorUnitId}", unitMoveTo.InitiatorUnitId);
                        return;
                    }

                    RunUnitMoveToCommand(executor, unitMoveTo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to move unit. InitiatorUnitId={InitiatorUnitId}", unitMoveTo.InitiatorUnitId);
                    throw;
                }
            });
        }

        public void AttackUnit(NetworkUnitAttack attack)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var executor = _gameStateLookupService.GetUnitEntity(attack.InitiatorUnitId);
                    if (executor == null)
                    {
                        _logger.LogError("Unable to find executor unit to perform unit attack command. InitiatorUnitId={InitiatorUnitId}", attack.InitiatorUnitId);
                        return;
                    }

                    RunUnitAttackCommand(executor, attack);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to attack unit. InitiatorUnitId={InitiatorUnitId}, TargetUnitId={TargetUnitId}", attack.InitiatorUnitId, attack.TargetUnitId);
                    throw;
                }
            });
        }

        public void UseAbility(NetworkAbilityUse networkAbilityUse)
        {
            try
            {
                _mainThreadAccessor.Post(() =>
                {
                    var executor = _gameStateLookupService.GetUnitEntity(networkAbilityUse.InitiatorUnitId);
                    if (executor == null)
                    {
                        _logger.LogError("Unable to find executor unit to perform unit ability use. InitiatorUnitId={InitiatorUnitId}", networkAbilityUse.InitiatorUnitId);
                        return;
                    }

                    RunUnitAbilityCommand(executor, networkAbilityUse);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to initiate UseAbility. InitiatorUnitId={InitiatorUnitId}, TargetUnitId={TargetUnitId}, AbilityId={AbilityId}", networkAbilityUse.InitiatorUnitId, networkAbilityUse.Target?.UnitId, networkAbilityUse.Ability.Id);
                throw;
            }
        }

        public bool CanRiderGetUp()
        {
            var canGetUp = Game.Instance.TurnBasedCombatController.CurrentTurn?.UnitCanGetUpOnCommand.Value ?? false;
            return canGetUp;
        }

        public bool IsRiderActive()
        {
            try
            {
                if (Game.Instance.TurnBasedCombatController.CurrentTurn?.Rider == null)
                {
                    return false;
                }

                var rider = Game.Instance.TurnBasedCombatController.CurrentTurn.Rider;
                var mount = Game.Instance.TurnBasedCombatController.CurrentTurn.Mount;
                var isRiderActing = Game.Instance.TurnBasedCombatController.CurrentTurn.m_RunningCommands.Count > 0
                    || Game.Instance.ProjectileController.HasLaunchedProjectile(rider, mount)
                    || Game.Instance.TurnBasedCombatController.CurrentTurn.IsMoving
                    || rider.Commands.HasAiCommand()
                    || rider.AreHandsBusyWithAnimation
                    || mount != null && mount.Commands.HasAiCommand();

                return isRiderActing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking if rider is active");
                return false;
            }
        }

        public void KillUnit(NetworkPlayer player, string unitId)
        {
            _mainThreadAccessor.Post(() =>
            {
                var unit = _gameStateLookupService.GetUnitEntity(unitId);
                if (unit == null || unit.State.IsFinallyDead)
                {
                    return;
                }

                _playerNotificationService.ShowWarningNotification(WellKnownKeys.GameNotifications.Combat.UnitAutokilled.Key, args: [unit.CharacterName, unit.UniqueId, player.Name]);
                GameHelper.KillUnit(unit);
            });
        }

        public Task<bool> StartCombatAsync(NetworkCombatState networkCombatState)
        {
            var taskCompletion = new TaskCompletionSource<bool>();

            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    if (Game.Instance.Player.IsInCombat)
                    {
                        taskCompletion.SetResult(false);
                        return;
                    }

                    var unitsInCombat = networkCombatState.Units.Select(u => _gameStateLookupService.GetUnitEntity(u.Id)).ToList();
                    foreach (UnitEntityData unitEntityData in unitsInCombat)
                    {
                        if (unitEntityData == null)
                        {
                            continue;
                        }

                        if (!unitEntityData.IsPlayerFaction && unitEntityData.IsPlayersEnemy)
                        {
                            foreach (UnitEntityData unitEntityData2 in Game.Instance.Player.PartyAndPets)
                            {
                                Game.Instance.UnitMemoryController.AddToMemory(unitEntityData, unitEntityData2);
                            }
                        }
                    }

                    taskCompletion.SetResult(true);
                    _logger.LogInformation("Combat has been forced to start");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to force combat start. UnitsCount={UnitsCount}", networkCombatState.Units.Count);
                    taskCompletion.SetResult(false);
                    throw;
                }
            });

            return taskCompletion.Task;
        }

        public Task<bool> EnsureUnitsInCombatAsync(List<NetworkUnit> units)
        {
            var taskCompletion = new TaskCompletionSource<bool>();

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            List<UnitEntityData> localUnits = [.. units.Select(u => _gameStateLookupService.GetUnitEntity(u.Id)).Where(x => x != null)];
            if (localUnits.Count != units.Count)
            {
                _logger.LogWarning("Waiting for all units to be available locally");
                while (localUnits.Count != units.Count && !timeout.IsCancellationRequested)
                {
                    localUnits = [.. units.Select(u => _gameStateLookupService.GetUnitEntity(u.Id)).Where(x => x != null)];
                }
            }

            if (timeout.IsCancellationRequested)
            {
                taskCompletion.SetResult(false);
                return taskCompletion.Task;
            }

            timeout.Cancel();

            _mainThreadAccessor.Post(() =>
            {
                AddUnitsToCombat(localUnits);
                taskCompletion.SetResult(true);
            });

            return taskCompletion.Task;
        }

        public List<NetworkUnit> GetParty()
        {
            var party = _gameStateLookupService.GetActualParty();
            var units = GetUnitsState(party);
            return units;
        }

        public void UpdateUnits(List<NetworkUnit> networkUnits)
        {
            foreach (var networkUnit in networkUnits)
            {
                try
                {
                    var unit = _gameStateLookupService.GetUnitEntity(networkUnit.Id);
                    UpdateUnit(unit, networkUnit, updatePosition: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while updating unit. UnitId={UnitId}", networkUnit.Id);
                }
            }
        }

        public List<NetworkUnit> GetUnitsInCombat()
        {
            try
            {
                List<UnitEntityData> unitsInCombat = [.. Game.Instance.State.Units.InCombat()];

                switch (Game.Instance.CurrentlyLoadedArea.name)
                {
                    case "Prologue_Caves_1":
                        var anevia = Game.Instance.State.Units.FirstOrDefault(u => string.Equals(u.CharacterName, "Anevia", StringComparison.OrdinalIgnoreCase));
                        if (anevia != null)
                        {
                            unitsInCombat.Add(anevia);
                        }
                        break;
                    default:
                        break;
                }

                var units = GetUnitsState(unitsInCombat);
                return units;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to get units in combat");
                throw;
            }
        }

        private List<NetworkUnit> GetUnitsState(List<UnitEntityData> unitEntities)
        {
            var units = new List<NetworkUnit>();
            foreach (var unitEntity in unitEntities)
            {
                var unit = new NetworkUnit
                {
                    Id = unitEntity.UniqueId,
                    Position = unitEntity.Position.ToNetworkVector3(),
                    Orientation = unitEntity.Orientation,
                    TurnBasedInfo = GetUnitTurnBasedInfo(unitEntity),
                    CombatState = GetUnitCombatState(unitEntity),
                    Descriptor = GetUnitDescriptor(unitEntity),
                    BuffCollection = _buffInteractionService.GetUnitBuffs(unitEntity)
                };

                var pitPart = unitEntity.Get<UnitPartInPit>();
                if (pitPart != null)
                {
                    unit.UnitPartInPit = new NetworkUnitPartInPit { CurrentRoundSeconds = pitPart.CurrentRoundSeconds };
                }

                var kineticist = unitEntity.Get<UnitPartKineticist>();
                if (kineticist != null)
                {
                    unit.UnitPartKineticist = new NetworkUnitPartKineticist { AcceptedBurn = kineticist.AcceptedBurn };
                }

                units.Add(unit);
            }

            return units;
        }

        private void AddUnitsToCombat(List<UnitEntityData> units)
        {
            foreach (UnitEntityData unit in units)
            {
                if (unit.IsInCombat)
                {
                    _logger.LogWarning("Unit is already in combat. UnitId={UnitId}", unit.UniqueId);
                    continue;
                }

                var notSurprised = UnitCombatJoinController.CalculateIsNotSurprised(unit);
                unit.JoinCombat(notSurprised);
            }

            _logger.LogInformation("Units have been added to combat. Units={Units}", units.Select(x => x.UniqueId));
        }

        private void UpdateAreaEffects(List<NetworkAreaEffect> areaEffects)
        {
            _logger.LogInformation("Updating area effects. AreaEffects={AreaEffects}", areaEffects);
            foreach (var areaEffect in areaEffects)
            {
                var localAreaEffect = _gameStateLookupService.GetAreaEffect(areaEffect);
                if (localAreaEffect == null)
                {
                    _logger.LogWarning("Unable to find area effect to update. Id={Id}, Name={Name}", areaEffect.Id, areaEffect.Name);
                    continue;
                }

                if (localAreaEffect.View == null)
                {
                    _logger.LogWarning("Skipping update for an area effect without view. Id={Id}", areaEffect.Id, areaEffect.Name);
                    continue;
                }

                var units = areaEffect.UnitsInside
                    .Select(x => new AreaEffectEntityData.UnitInfo
                    {
                        Reference = _gameStateLookupService.GetUnitEntity(x),
                        InsideThisTick = true
                    })
                    .ToList();
                localAreaEffect.m_UnitsInside = units;

                var position = areaEffect.Position.ToUnityVector3();
                localAreaEffect.View.transform.position = position;
                localAreaEffect.m_Position = position;

                localAreaEffect.UpdateViewAndUnits();
                _logger.LogInformation("Area effect has been updated. Id={Id}, Name={Name}, Position={Position}, UnitsUnside={UnitsUnside}", localAreaEffect.UniqueId, localAreaEffect.Blueprint.name, localAreaEffect.Position, localAreaEffect.m_UnitsInside.Select(x => x.Reference.UniqueId));
            }
        }

        private void TriggerAreaEffects(List<NetworkAreaEffect> triggeredAreaEffects)
        {
            _logger.LogInformation("Triggering area effects. AreaEffects={AreaEffects}", triggeredAreaEffects);

            foreach (var triggered in triggeredAreaEffects)
            {
                var areaEffect = _gameStateLookupService.GetAreaEffect(triggered);
                if (areaEffect == null)
                {
                    _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.Combat.AreaEffects.Missing.Key, CombatTextSeverity.Debug, triggered.Name, triggered.Id);
                    _logger.LogWarning("Unable to find area effect to trigger. Id={Id}", triggered.Id);
                    continue;
                }

                areaEffect.Blueprint.HandleRound(areaEffect.m_Context, areaEffect);
                _playerNotificationService.AddCombatText(WellKnownKeys.GameNotifications.Combat.AreaEffects.Triggered.Key, CombatTextSeverity.Debug, triggered.Name, triggered.Id);
                _logger.LogInformation("Area effect has been triggered. Id={Id}, Name={Name}", areaEffect.UniqueId, areaEffect.Blueprint.name);
            }
        }

        private void SetTurnMovementLimit(string rawMovementLimit, UnitEntityData executor)
        {
            var turn = Game.Instance.TurnBasedCombatController.CurrentTurn;
            if (!string.IsNullOrEmpty(rawMovementLimit) && turn != null)
            {
                Enum.TryParse<TurnController.MovementLimit>(rawMovementLimit, true, out var movementLimit);
                turn.CurrentMovementLimit = movementLimit;
                _logger.LogInformation("Unit movement limit has been updated. ExecutorUnitId={ExecutorUnitId}, Limit={Limit}", executor.UniqueId, movementLimit);
            }
        }

        private ForcedPath CreateForcedPath(List<NetworkVector3> path)
        {
            if (path == null || path.Count == 0)
            {
                return null;
            }

            var vectorPath = path.Select(v => v.ToUnityVector3()).ToList();
            var forcedPath = new ForcedPath(vectorPath);
            return forcedPath;
        }

        private NetworkUnitDescriptor GetUnitDescriptor(UnitEntityData combatUnit)
        {
            var descriptor = new NetworkUnitDescriptor
            {
                Damage = combatUnit.Descriptor.Damage,
                Stats = new NetworkCharacterStats
                {
                },
                State = new NetworkUnitState
                {
                    IsCharging = combatUnit.Descriptor.State.IsCharging
                }
            };
            return descriptor;
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

            var unitCombatState = combatUnit.CombatState;
            var state = new NetworkUnitCombatState
            {
                EngagedUnits = [.. unitCombatState.EngagedUnits.Select(x => x.UniqueId)],
                EngagedBy = [.. unitCombatState.EngagedBy.Select(x => x.UniqueId)]
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

                    UpdateUnit(unit, networkUnit, updatePosition: true);
                    UpdateUnitCombatState(unit, networkUnit.CombatState);

                    if (!requiresFullUpdate)
                    {
                        _buffInteractionService.UpdateUnitBuffs(unit, networkUnit.BuffCollection);
                    }

                    if (requiresFullUpdate)
                    {
                        UpdateUnitState(unit, networkUnit.Descriptor.State);
                        UpdateUnitTurnBasedInfo(unit, networkUnit.TurnBasedInfo);
                    }
                }

                UpdateEngagements(unitsToUpdate);

                UpdateAreaEffects(networkCombatState.AreaEffects);

                _logger.LogInformation("Combat state has been updated. RoundNumber={RoundNumber}, UnitsCount={UnitsCount}, IsFullUpdate={IsFullUpdate}", networkCombatState.RoundNumber, networkCombatState.Units.Count, requiresFullUpdate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to update combat state. RoundNumber={RoundNumber}, UnitsCount={UnitsCount}, IsFullUpdate={IsFullUpdate}", networkCombatState.RoundNumber, networkCombatState.Units.Count, requiresFullUpdate);
                throw;
            }
        }

        private void UpdateUnitCombatState(UnitEntityData unit, NetworkUnitCombatState combatState)
        {
            unit.CombatState.NotSurprised = combatState.NotSurprised;
        }

        private void UpdateUnit(UnitEntityData unit, NetworkUnit remoteUnit, bool updatePosition)
        {
            if (unit == null || remoteUnit == null)
            {
                _logger.LogWarning("Unable to update missing unit. UnitId={UnitId}, NetworkUnitId={NetworkUnitId}", unit.UniqueId, remoteUnit.Id);
                return;
            }

            UpdateUnitParts(unit, remoteUnit);
            UpdateUnitHealth(unit, remoteUnit);

            if (updatePosition)
            {
                UpdateUnitPosition(unit, remoteUnit);
            }
        }

        private void UpdateUnitParts(UnitEntityData unit, NetworkUnit networkUnit)
        {
            UpdateUnitPartKineticist(unit, networkUnit);
            UpdateUnitPartInPit(unit, networkUnit);
        }

        private void UpdateUnitPartKineticist(UnitEntityData unit, NetworkUnit networkUnit)
        {
            if (networkUnit.UnitPartKineticist == null)
            {
                return;
            }

            var unitPart = unit.Get<UnitPartKineticist>();
            if (unitPart == null)
            {
                _logger.LogError("UnitPartKineticist doesn't exist on the unit. UnitId={UnitId}", unit.UniqueId);
                return;
            }

            if (unitPart.AcceptedBurn != networkUnit.UnitPartKineticist.AcceptedBurn)
            {
                var previousValue = unitPart.AcceptedBurn;
                unitPart.AcceptedBurn = networkUnit.UnitPartKineticist.AcceptedBurn;

                EventBus.RaiseEvent<IKineticistBurnValueHandler>(unit, x => x.HandleKineticistBurnValueChanged(unitPart, previousValue, null));
                EventBus.RaiseEvent<IKineticistGlobalHandler>(x => x.HandleKineticistBurnValueChanged(unitPart, previousValue, null));
                _logger.LogInformation("UnitPartKineticist burn has been updated. UnitId={UnitId}, AcceptedBurn={AcceptedBurn}", unit.UniqueId, unitPart.AcceptedBurn);
            }
        }

        private void UpdateUnitPartInPit(UnitEntityData unit, NetworkUnit networkUnit)
        {
            if (networkUnit.UnitPartInPit == null)
            {
                return;
            }

            var unitPart = unit.Get<UnitPartInPit>();
            if (unitPart != null)
            {
                unitPart.CurrentRoundSeconds = networkUnit.UnitPartInPit.CurrentRoundSeconds;
                unitPart.State = networkUnit.UnitPartInPit.State;
                _logger.LogInformation("UnitPartInPit has been updated. UnitId={UnitId}, State={State}, CurrentRoundSeconds={CurrentRoundSeconds}", unit.UniqueId, unitPart.State, unitPart.CurrentRoundSeconds);
            }
        }

        private void UpdateUnitState(UnitEntityData unit, NetworkUnitState state)
        {
            unit.State.IsCharging = state.IsCharging;
        }

        private void UpdateUnitHealth(UnitEntityData unit, NetworkUnit networkUnit)
        {
            unit.Damage = networkUnit.Descriptor.Damage;
        }

        private void RunUnitMoveToCommand(UnitEntityData executorUnit, NetworkUnitMoveTo networkUnitMoveTo)
        {
            var destination = networkUnitMoveTo.Destination.ToUnityVector3();
            var command = new UnitMoveTo(destination);
            SetCommandPath(networkUnitMoveTo.VectorPath, command);
            SetTurnMovementLimit(networkUnitMoveTo.MovementLimit, executorUnit);

            executorUnit.Commands.Run(command);
            _logger.LogInformation("Unit MoveTo command has been initiated. UnitId={UnitId}, Destination={Destination}, Path={Path} MovementLimit={MovementLimit}",
                networkUnitMoveTo.InitiatorUnitId, networkUnitMoveTo.Destination, networkUnitMoveTo.VectorPath, networkUnitMoveTo.MovementLimit);
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
            command.IsCharge = networkUnitAttack.IsCharge;
            command.CreatedByPlayer = true;

            var turn = Game.Instance.TurnBasedCombatController.CurrentTurn;
            if (turn != null)
            {
                turn.m_AttackMode = command.ForceFullAttack ? TurnController.AttackMode.FullAttack : TurnController.AttackMode.SingleAttack;
            }

            SetCommandPath(networkUnitAttack.VectorPath, command);
            SetTurnMovementLimit(networkUnitAttack.MovementLimit, executorUnit);

            _logger.LogInformation("Unit Attack command has been initiated. UnitId={UnitId}, TargetUnitId={TargetUnitId}, ForceFullAttack={ForceFullAttack}, Path={Path}, AttackMode={AttackMode}, MovementLimit={MovementLimit}",
                executorUnit.UniqueId, networkUnitAttack.TargetUnitId, command.ForceFullAttack, command.ForcedPath?.vectorPath, turn?.m_AttackMode, turn?.CurrentMovementLimit);
            executorUnit.Commands.Run(command);
        }

        private void RunUnitAbilityCommand(UnitEntityData executorUnit, NetworkAbilityUse networkAbilityUse)
        {
            var abilityData = _gameStateLookupService.FindAbility(executorUnit, networkAbilityUse.Ability);
            if (abilityData == null)
            {
                _logger.LogError("Unable to run unit ability command due to missing ability. UnitId={UnitId}, AbilityId={AbilityId}, SpellbookBlueprintId={SpellbookBlueprintId}", executorUnit.UniqueId, networkAbilityUse.Ability.Id, networkAbilityUse.Ability.SpellbookId);
                return;
            }

            abilityData.ParamSpellbook = _gameStateLookupService.GetSpellbook(executorUnit, networkAbilityUse.Ability.ParamSpellBookId);
            abilityData.ParamSpellLevel = networkAbilityUse.Ability.ParamSpellLevel;

            if (networkAbilityUse.Ability.ParamSpellSlot != null)
            {
                var param = networkAbilityUse.Ability.ParamSpellSlot;
                var spellSlotSpellBook = _gameStateLookupService.GetSpellbook(executorUnit, param.SpellbookId);
                abilityData.ParamSpellSlot = _gameStateLookupService.GetSpellSlot(spellSlotSpellBook, param.Slot, param.SpellLevel);
            }

            if (!Enum.TryParse<UnitCommand.CommandType>(networkAbilityUse.CommandType, true, out var commandType))
            {
                _logger.LogWarning("Unable to parse command type. Type={Type}", networkAbilityUse.CommandType);
                commandType = UnitCommand.CommandType.Standard;
            }
            var target = CreateTargetWrapper(networkAbilityUse.Target);

            RunUnitAbilityCommand(executorUnit, abilityData, target, networkAbilityUse.VectorPath, commandType, networkAbilityUse.MovementLimit);
        }

        private void RunUnitAbilityCommand(UnitEntityData executorUnit, AbilityData abilityData, TargetWrapper targetWrapper, List<NetworkVector3> vectorPath, UnitCommand.CommandType commandType, string rawMovementLimit)
        {
            var command = UnitUseAbility.CreateCastCommand(abilityData, targetWrapper, commandType);
            command.CreatedByPlayer = true;

            SetCommandPath(vectorPath, command);
            SetTurnMovementLimit(rawMovementLimit, executorUnit);

            _logger.LogInformation("Unit UseAbility command has been initiated. UnitId={UnitId}, TargetUnitId={TargetUnitId}, TargetPoint={TargetPoint}, AbilityId={AbilityId}, AbilityName={AbilityName}", executorUnit.UniqueId, targetWrapper.Point, targetWrapper.Unit?.UniqueId, abilityData.UniqueId, abilityData.NameForAcronym);
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

        private void UpdateEngagements(Dictionary<NetworkUnit, UnitEntityData> unitsToUpdate)
        {
            // engagement is configured for units pair so we need to clear existing lists before syncing
            // also EngagedX lists could contain units that are not in combat right now - therefore not present in the base list
            var engagedUnits = new Dictionary<string, UnitEntityData>();
            foreach (var (networkUnit, unit) in unitsToUpdate)
            {
                AddUnitForEngagementClearence([unit.UniqueId], engagedUnits);
                AddUnitForEngagementClearence(networkUnit.CombatState?.EngagedBy ?? [], engagedUnits);
                AddUnitForEngagementClearence(networkUnit.CombatState?.EngagedUnits ?? [], engagedUnits);
            }

            foreach (var (_, unitToClear) in engagedUnits)
            {
                unitToClear.CombatState.m_EngagedBy.Clear();
                unitToClear.CombatState.m_EngagedUnits.Clear();
                unitToClear.CombatState.DisengageAttackTargets.Clear();
            }

            foreach (var (networkUnit, unit) in unitsToUpdate)
            {
                foreach (var engageTargetId in networkUnit.CombatState.EngagedUnits)
                {
                    var engageTarget = _gameStateLookupService.GetUnitEntity(engageTargetId);
                    if (engageTarget == null)
                    {
                        _logger.LogError("Unable to engage missing unit. UnitId={UnitId}, EngageTargetId={EngageTargetId}", unit.UniqueId, engageTargetId);
                        continue;
                    }

                    unit.CombatState.Engage(engageTarget);
                }

                _logger.LogDebug("Unit engagement has been updated. UnitId={UnitId}, EngagedWith={EngagedWith}, EngagedBy={EngagedBy}, HostEngagedWith={HostEngagedWith}, HostEngagedBy={HostEngagedBy}", unit.UniqueId, string.Join(";", unit.CombatState.m_EngagedUnits.Select(x => x.Key.UniqueId)), string.Join(";", unit.CombatState.m_EngagedBy.Select(x => x.Key.UniqueId)), networkUnit.CombatState.EngagedUnits, networkUnit.CombatState.EngagedBy);
            }
        }

        private void AddUnitForEngagementClearence(List<string> units, Dictionary<string, UnitEntityData> unitsToClear)
        {
            foreach (var unitId in units)
            {
                if (unitsToClear.ContainsKey(unitId))
                {
                    continue;
                }

                var unitData = _gameStateLookupService.GetUnitEntity(unitId);
                if (unitData != null)
                {
                    unitsToClear.Add(unitId, unitData);
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
