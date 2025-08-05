using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;

namespace WOTR.Multiplayer.Playground.Core.Dummies
{
    public class DummyDiceRollStorage : IDiceRollStorage
    {
        private readonly List<RollValueBase> _values;

        public DummyDiceRollStorage(List<RollValueBase> values)
        {
            _values = values;
        }

        public void Add(int rollId, List<long> claimingList, RollValueBase roll)
        {
        }

        public TValue Get<TValue>(int rollId, long playerId)
            where TValue : RollValueBase
        {
            return (TValue)_values.FirstOrDefault();
        }

        public Task<TValue> GetAsync<TValue>(int rollId, long playerId, TimeSpan? waitForRollTimeout) where TValue : RollValueBase
        {
            return Task.FromResult(Get<TValue>(rollId, playerId));
        }

        public void Reset()
        {
        }
    }
}
