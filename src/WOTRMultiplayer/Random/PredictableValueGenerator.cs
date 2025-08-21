using System.Collections.Concurrent;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Random
{
    public class PredictableValueGenerator : IValueGenerator
    {
        private readonly ConcurrentDictionary<string, UniqueIdCounters> _entityCounters = new();
        private readonly ConcurrentDictionary<int, System.Random> _seedGenerators = new();
        private readonly ILogger<PredictableValueGenerator> _logger;
        private readonly IGameInteractionService _gameInteractionService;
        private readonly IHashService _hashService;

        public PredictableValueGenerator(
            ILogger<PredictableValueGenerator> logger,
            IGameInteractionService gameInteractionService,
            IHashService hashService)
        {
            _logger = logger;
            _gameInteractionService = gameInteractionService;
            _hashService = hashService;
        }

        public int Range(int seed, int minInclusive, int maxExclusive)
        {
            var generator = _seedGenerators.GetOrAdd(seed, k => new System.Random(k));
            var result = generator.Next(minInclusive, maxExclusive);
            return result;
        }

        public void Reset(string gameId)
        {
            _entityCounters.TryRemove(gameId, out _);
            _logger.LogInformation("Counters have been cleared. GameId={GameId}", gameId);
        }

        public string GenerateUniqueId(UniqueIdType uniqueIdType, string gameId, string identifier)
        {
            try
            {
                var counter = _entityCounters.GetOrAdd(gameId ?? "main-menu", k => new UniqueIdCounters());
                var hashed = _hashService.Murmur3(identifier).ToString();

                if (!counter.NameIdentifiers.TryGetValue(hashed, out uint nameCounter))
                {
                    nameCounter = 0;
                }

                var prefix = uniqueIdType.GetAttributeOfType<DescriptionAttribute>()?.Description ?? uniqueIdType.ToString();
                string id;
                do
                {
                    nameCounter += 1;
                    id = $"{prefix}_{hashed}_{nameCounter}";
                }
                while (_gameInteractionService.GetEntity(id) != null);

                counter.NameIdentifiers.AddOrUpdate(hashed, nameCounter, (key, existing) => nameCounter);
                return id;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Unable to generate unique id. UniqueIdType={UniqueIdType}, Identifier={Identifier}", uniqueIdType, identifier);
                throw;
            }
        }
    }
}
