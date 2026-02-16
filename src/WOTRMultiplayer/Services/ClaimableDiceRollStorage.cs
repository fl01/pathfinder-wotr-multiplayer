using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Entities.Rolls.Claiming;
using WOTRMultiplayer.Entities.Rolls.Claiming.Values;

namespace WOTRMultiplayer.Services
{
    public class ClaimableDiceRollStorage : IDiceRollStorage
    {
        private readonly TimeSpan _defaultRetrieveDelay = TimeSpan.FromMilliseconds(10);
        private readonly object _actionLock = new();
        private readonly ConcurrentDictionary<int, ClaimableDiceRollEntry> _rolls = new();

        private readonly ILogger<ClaimableDiceRollStorage> _logger;

        public ClaimableDiceRollStorage(ILogger<ClaimableDiceRollStorage> logger)
        {
            _logger = logger;
        }

        public TValue Get<TValue>(int rollId, long playerId)
            where TValue : RollValueBase
        {
            if (!_rolls.TryGetValue(rollId, out var entry))
            {
                return null;
            }

            lock (_actionLock)
            {
                foreach (var claimableValue in entry.Rolls)
                {
                    if (claimableValue.ClaimingList.TryRemove(playerId, out _))
                    {
                        _logger.LogDebug("Claimed roll value. RollId={RollId}, PlayerId={PlayerId}, OrderId={OrderId}, RollType={RollType}", rollId, playerId, claimableValue.OrderId, claimableValue.Roll.GetType().Name);
                        return (TValue)claimableValue.Roll;
                    }
                }
            }

            return null;
        }

        public async Task<TValue> GetAsync<TValue>(int rollId, long playerId, TimeSpan? waitForRollTimeout)
            where TValue : RollValueBase
        {
            var timeoutTask = waitForRollTimeout == null ? Task.CompletedTask : Task.Delay(waitForRollTimeout.Value);
            TValue result;
            do
            {
                result = Get<TValue>(rollId, playerId);
                if (result == null)
                {
                    await Task.Delay(_defaultRetrieveDelay);
                }
            }
            while (result == null && !timeoutTask.IsCompleted);

            return result;
        }

        public void UndoClaiming(long playerId)
        {
            lock (_actionLock)
            {
                foreach (var value in _rolls.Values)
                {
                    foreach (var roll in value.Rolls)
                    {
                        roll.ClaimingList.TryAdd(playerId, true);
                    }
                }
            }

            _logger.LogWarning("Rolls have been unclaimed. PlayerId={PlayerId}", playerId);
        }

        public void Add(int rollId, List<long> claimingList, RollValueBase roll)
        {
            lock (_actionLock)
            {
                if (!_rolls.TryGetValue(rollId, out var entry))
                {
                    AddRollEntry(rollId, claimingList, roll, false);
                    return;
                }

                if (entry.IsClaimed)
                {
                    _rolls.TryRemove(rollId, out _);
                    AddRollEntry(rollId, claimingList, roll, true);
                    return;
                }

                var extraClaimableRoll = CreateClaimableRoll(claimingList, roll);
                extraClaimableRoll.OrderId = entry.Rolls.Count;
                entry.Rolls.Add(extraClaimableRoll);
                _logger.LogWarning("Appended claimable roll value to existing entry. RollId={RollId}, RollType={RollType}, OrderId={OrderId}, ClaimingListCount={ClaimingListCount}", rollId, roll.GetType().Name, extraClaimableRoll.OrderId, claimingList.Count);
            }
        }

        public void Reset()
        {
            _rolls.Clear();
            _logger.LogInformation("All previous rolls have been removed");
        }

        private void AddRollEntry(int rollId, List<long> claimingList, RollValueBase roll, bool isReplacement)
        {
            var rollValue = CreateClaimableRoll(claimingList, roll);
            var entry = new ClaimableDiceRollEntry();
            entry.Rolls.Add(rollValue);
            _rolls.TryAdd(rollId, entry);
            _logger.LogInformation("Created claimable roll value. RollId={RollId}, RollType={RollType}, OrderId={OrderId}, ClaimingListCount={ClaimingListCount}, RollValue={RollValue}, IsReplacement={IsReplacement}", rollId, roll.GetType().Name, rollValue.OrderId, claimingList.Count, rollValue.Roll, isReplacement);
        }

        private ClaimableDiceRollValue<RollValueBase> CreateClaimableRoll(List<long> claimingList, RollValueBase roll)
        {
            var rollValue = new ClaimableDiceRollValue<RollValueBase>
            {
                Roll = roll,
                ClaimingList = new ConcurrentDictionary<long, bool>(claimingList.Select(x => new KeyValuePair<long, bool>(x, true))),
                OrderId = 0
            };
            return rollValue;
        }
    }
}
