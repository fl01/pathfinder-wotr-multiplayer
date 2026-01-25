using System.Threading.Tasks;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;

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

        public void EndTurnBasedCombatTurn()
        {
        }

        public NetworkCombatState GetCombatState()
        {
            return null;
        }

        public void StartTurnBasedCombatTurn(string unitId)
        {
        }

        public Task UpdateCombatStateAsync(NetworkCombatState networkCombatState, bool requiresFullUpdate)
        {
            return Task.CompletedTask;
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

        public void UpdateIsInCombatStatus()
        {
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

        public void UseAbility(NetworkAbility networkAbility)
        {
        }
    }
}
