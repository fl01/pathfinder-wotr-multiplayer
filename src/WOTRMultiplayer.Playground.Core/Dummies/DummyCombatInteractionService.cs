using System.Threading.Tasks;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities.Combat;

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

        public int GetCrusadeArmyCombatSeed()
        {
            return 0;
        }
    }
}
