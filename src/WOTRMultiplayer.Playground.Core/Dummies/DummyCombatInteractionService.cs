using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.AreaEffects;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Units;

namespace WOTRMultiplayer.Playground.Core.Dummies
{
    public class DummyCombatInteractionService : ICombatInteractionService
    {
        public bool IsCombatTurnFinished()
        {
            return false;
        }

        public void DelayCombatTurn(string unitId, string targetUnitId)
        {
        }

        public void EndCombatTurn(string unitId)
        {
        }

        public NetworkCombatState GetCombatState()
        {
            return null;
        }

        public void InitializeCrusadeArmyCombat()
        {
        }

        public int GetCrusadeArmyCombatAreaSeed()
        {
            return 0;
        }

        public void RunTacticalUnitAttackCommand(NetworkTacticalUnitAttackCommand tacticalUnitAttackCommand)
        {
        }

        public void RunTacticalUnitUseAbilityCommand(NetworkTacticalUnitUseAbilityCommand tacticalUnitUseAbilityCommand)
        {
        }

        public void RunTacticalUnitMoveToCommand(NetworkTacticalUnitMoveToCommand tacticalUnitMoveToCommand)
        {
        }

        public bool IsInCombat()
        {
            return false;
        }

        public bool IsInCrusadeTacticalCombat()
        {
            return false;
        }

        public void UseTacticalCombatTotalDefense()
        {
        }

        public void PostponeTacticalCombatTurn()
        {
        }

        public void RetreatFromTacticalCombat()
        {
        }

        public void AttackUnit(NetworkUnitAttack attack)
        {
        }

        public void UseAbility(NetworkAbilityUse networkAbilityUse)
        {
        }

        public Task<bool> StartCombatAsync(NetworkCombatState networkCombatState)
        {
            return Task.FromResult(false);
        }

        public bool IsRiderActive()
        {
            return false;
        }

        public List<NetworkUnit> GetUnitsInCombat()
        {
            return [];
        }

        public bool IsInCombat(string unitId)
        {
            return false;
        }

        public Task UpdateCombatStateAsync(NetworkCombatState networkCombatState, List<NetworkAreaEffect> areaEffects, bool requiresFullUpdate)
        {
            return Task.CompletedTask;
        }

        public void UpdateUnits(List<NetworkUnit> networkUnits, bool updatePosition)
        {
        }

        public List<NetworkUnit> GetParty()
        {
            return [];
        }

        public bool IsRiderActiveAndHasActions()
        {
            return false;
        }

        public void MoveUnit(NetworkUnitMoveTo unitMoveTo)
        {
        }

        public Task<bool> UpdateUnitsAsync(List<NetworkUnit> networkUnits, bool updatePosition)
        {
            return Task.FromResult(false);
        }

        public bool AreThereAnyProjectilesLaunchedByParty()
        {
            return false;
        }

        public void InteractWithUnit(NetworkUnitInteractWithUnit interaction)
        {
        }

        public void LootUnit(NetworkUnitLootUnit networkUnitLootUnit)
        {
        }

        public void AddUnitsToCombat(List<string> units)
        {
        }

        public void ForceResetCombat()
        {
        }

        public void MakeUnitTargetable(string unitId)
        {
        }

        public void MakeUnitUntargetable(string unitId)
        {
        }

        public bool IsCombatInitialized()
        {
            return false;
        }

        public bool KillUnit(NetworkPlayer player, string unitId)
        {
            return false;
        }

        public void StartTurnBasedCombatTurn(string unitId, Action onInvalidUnit = null)
        {
        }

        public Task ExcludeUnitsFromCombatAsync(List<string> units)
        {
            return Task.FromResult(false);
        }

        public Task<List<string>> EnsureUnitsInCombatAsync(List<string> units)
        {
            return Task.FromResult(units);
        }

        public Task ForceResetCombatAsync()
        {
            return Task.FromResult(true);
        }

        public void SetTacticalCombatAcceleration(bool isAccelerated)
        {
        }

        public Task KillUnitAndResetTurnAsync(NetworkPlayer networkPlayer, string unitId)
        {
            return Task.CompletedTask;
        }
    }
}
