using System.Threading.Tasks;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface ICombatInteractionService
    {
        bool IsInCombat();

        bool IsInCrusadeTacticalCombat();

        void UpdateIsInCombatStatus();

        NetworkCombatState GetCombatState();

        void StartTurnBasedCombatTurn(string unitId);

        void EndTurnBasedCombatTurn();

        bool IsCombatTurnFinished();

        void DelayCombatTurn(string unitId, string targetUnitId);

        Task UpdateCombatStateAsync(NetworkCombatState networkCombatState, bool requiresFullUpdate);

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

        void KillUnit(NetworkPlayer player, string unitId);

        bool CanRiderGetUp();

        bool IsRiderActive();
    }
}
