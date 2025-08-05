using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IDiceRollStorage
    {
        TValue Get<TValue>(int rollId, long playerId)
            where TValue : RollValueBase;

        Task<TValue> GetAsync<TValue>(int rollId, long playerId, TimeSpan? waitForRollTimeout)
            where TValue : RollValueBase;

        void Add(int rollId, List<long> claimingList, RollValueBase roll);

        void Reset();
    }
}
