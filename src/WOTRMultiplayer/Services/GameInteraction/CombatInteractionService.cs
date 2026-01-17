using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kingmaker;
using Kingmaker.Armies.TacticalCombat.Blueprints;
using Kingmaker.Armies.TacticalCombat.Controllers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using UniRx;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Units;

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
    }
}
