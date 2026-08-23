using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.AreaEffects;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Units;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface ICombatInteractionService
    {
        bool IsInCombat();

        bool IsInCombat(string unitId);

        bool IsInCrusadeTacticalCombat();

        NetworkCombatState GetCombatState();

        List<NetworkUnit> GetUnitsInCombat();

        List<NetworkUnit> GetParty();

        void UpdateUnits(List<NetworkUnit> networkUnits, bool updatePosition);

        Task<bool> UpdateUnitsAsync(List<NetworkUnit> networkUnits, bool updatePosition);

        void StartTurnBasedCombatTurn(string unitId, Action onInvalidUnit = null);

        void EndCombatTurn(string unitId);

        bool IsCombatTurnFinished();

        void DelayCombatTurn(string unitId, string targetUnitId);

        Task UpdateCombatStateAsync(NetworkCombatState networkCombatState, List<NetworkAreaEffect> areaEffects, bool requiresFullUpdate);

        void InitializeCrusadeArmyCombat();

        int GetCrusadeArmyCombatAreaSeed();

        void RunTacticalUnitAttackCommand(NetworkTacticalUnitAttackCommand tacticalUnitAttackCommand);

        void RunTacticalUnitUseAbilityCommand(NetworkTacticalUnitUseAbilityCommand tacticalUnitUseAbilityCommand);

        void RunTacticalUnitMoveToCommand(NetworkTacticalUnitMoveToCommand tacticalUnitMoveToCommand);

        void UseTacticalCombatTotalDefense();

        void PostponeTacticalCombatTurn();

        void RetreatFromTacticalCombat();

        void AttackUnit(NetworkUnitAttack attack);

        void UseAbility(NetworkAbilityUse networkAbilityUse);

        Task<bool> StartCombatAsync(NetworkCombatState networkCombatState);

        Task<List<string>> EnsureUnitsInCombatAsync(List<string> units);

        void AddUnitsToCombat(List<string> units);

        bool KillUnit(NetworkPlayer player, string unitId);

        bool IsRiderActive();

        void MoveUnit(NetworkUnitMoveTo unitMoveTo);

        void MakeUnitTargetable(string unitId);

        void MakeUnitUntargetable(string unitId);

        bool AreThereAnyProjectilesLaunchedByParty();

        void InteractWithUnit(NetworkUnitInteractWithUnit networkUnitInteractWithUnit);

        void LootUnit(NetworkUnitLootUnit networkUnitLootUnit);

        void ForceResetCombat();

        Task ForceResetCombatAsync();

        bool IsCombatInitialized();

        Task ExcludeUnitsFromCombatAsync(List<string> units);

        void SetTacticalCombatAcceleration(bool isAccelerated);

        Task KillUnitAndResetTurnAsync(NetworkPlayer networkPlayer, string unitId);
    }
}
