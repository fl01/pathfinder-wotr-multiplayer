using System;
using System.Threading.Tasks;
using WOTRMultiplayer.MP.Entities.Rolls;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IDiceRollStorage
    {
        bool Save(NetworkDiceRoll rollDice);

        NetworkDiceRoll Get(int rollId, long playerId, bool ensureCompleted = true);

        int GetUniqueId(NetworkDiceRoll roll);

        void Reset();

        void Reset<T>()
            where T : NetworkDiceRoll;

        Task<NetworkDiceRoll> GetAsync(int rollId, long playerId, TimeSpan? waitForRollTimeout);
    }
}
