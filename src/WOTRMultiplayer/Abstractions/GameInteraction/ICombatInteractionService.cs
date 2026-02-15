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

        void UpdateIsInCombatStatus();

        NetworkCombatState GetCombatState();

        List<NetworkUnit> GetUnitsInCombat();

        List<NetworkUnit> GetParty();

        void UpdateUnits(List<NetworkUnit> networkUnits);

        void StartTurnBasedCombatTurn(string unitId);

        void EndTurnBasedCombatTurn(bool isAI);

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

        Task<bool> EnsureUnitsInCombatAsync(List<NetworkUnit> units);

        void AddUnitsToCombat(List<string> units);

        void KillUnit(NetworkPlayer player, string unitId);

        bool CanRiderGetUp();

        bool IsRiderActive();

        void ExecuteAIAction(NetworkAIAction aiAction);
    }
}
