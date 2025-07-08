using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.MP.Entities.Rolls;

namespace WOTRMultiplayer.MP
{
    public class DiceRollStorage : IDiceRollStorage
    {
        private readonly ConcurrentDictionary<int, DiceRollEntry> _rolls = new();
        private readonly ILogger<DiceRollStorage> _logger;
        private readonly IHashService _hashService;

        public DiceRollStorage(
            ILogger<DiceRollStorage> logger,
            IHashService hashService)
        {
            _logger = logger;
            _hashService = hashService;
        }

        public void Add(NetworkDiceRoll diceRoll)
        {
            try
            {
                var rollId = GetUniqueId(diceRoll);
                var entry = new DiceRollEntry(diceRoll);
                if (!_rolls.TryAdd(rollId, entry))
                {
                    _logger.LogError("Collision has been detected. Type={type}, RollId={rollId}", diceRoll.GetType().Name, rollId);
                    return;
                }

                _logger.LogInformation("Roll has been preserved. Type={type}, UniqueId={rollId}, Result={result}, TotalBonus={totalBonus}, Initiator={initiator}, HistoryCount={historyCount}", diceRoll.GetType().Name, rollId, diceRoll.Result, diceRoll.TotalModifiersBonus, diceRoll.InitiatorId, diceRoll.RollHistory.Count);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Unable to add roll");
                throw;
            }
        }

        public NetworkDiceRoll Get(int rollId, long playerId)
        {
            try
            {
                if (!_rolls.TryGetValue(rollId, out var entry))
                {
                    _logger.LogWarning("Requested roll is missing. Id={id}", rollId);
                    return null;
                }

                entry.RetrieveHistory.AddOrUpdate(playerId, 1, (key, value) =>
                {
                    var accessCount = value++;
                    _logger.LogWarning("Same roll has been retrieved more than once by the same player. PlayerId={playerId}, RollId={rollId}, AccessCount={accessCount}", playerId, rollId, accessCount);
                    return accessCount;
                });

                return entry.Roll;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Unable retrieve roll. RollId={rollId}, PlayerId={playerId}", rollId, playerId);
                throw;
            }
        }

        public int GetUniqueId(NetworkDiceRoll roll)
        {
            var rawRollId = roll.GetIdString();
            var rollId = _hashService.Murmur3(rawRollId);
            return rollId;
        }

        public void Reset()
        {
            _rolls.Clear();
            _logger.LogInformation("All rolls have been cleared");
        }

        public void Reset<T>()
            where T : NetworkDiceRoll
        {
            var rollsToRemove = _rolls.Where(x => x.Value.Roll is T).ToList();
            foreach (var roll in rollsToRemove)
            {
                _rolls.TryRemove(roll.Key, out _);
            }

            _logger.LogInformation("Typed rolls have been cleared. RollType={rollType}, RemovedCount={removedCount}", typeof(T), rollsToRemove.Count);
        }
    }
}
