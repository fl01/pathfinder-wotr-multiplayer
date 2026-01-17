using System.Threading.Tasks;
using WOTRMultiplayer.Entities.Combat;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface ICombatInteractionService
    {
        NetworkCombatState GetCombatState();

        void StartTurnBasedCombatTurn(string unitId);

        void EndTurnBasedCombatTurn();

        bool IsCombatTurnFinished();

        void DelayCombatTurn(string unitId, string targetUnitId);

        Task UpdateCombatStateAsync(NetworkCombatState networkCombatState, bool requiresFullUpdate);

        void InitializeCrusadeArmyCombat();

        int GetCrusadeArmyCombatSeed();
    }
}
