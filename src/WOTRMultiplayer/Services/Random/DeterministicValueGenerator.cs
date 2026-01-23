using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Services.Random
{
    public class DeterministicValueGenerator : IValueGenerator
    {
        private readonly ConcurrentDictionary<string, UniqueIdCounters> _entityCounters = new();
        private readonly ConcurrentDictionary<int, Seed> _seedGenerators = new();
        private readonly ILogger<DeterministicValueGenerator> _logger;
        private readonly IGameInteractionService _gameInteractionService;
        private readonly IHashService _hashService;

        public DeterministicValueGenerator(
            ILogger<DeterministicValueGenerator> logger,
            IGameInteractionService gameInteractionService,
            IHashService hashService)
        {
            _logger = logger;
            _gameInteractionService = gameInteractionService;
            _hashService = hashService;
        }

        public Guid CreateGuid(SeedLifetime seedLifetime, string seed)
        {
            var actualSeed = _hashService.Murmur3(seed);
            var generator = GetSeed(seedLifetime, actualSeed);
            var guidBytes = new byte[16];
            generator.Random.NextBytes(guidBytes);
            var guid = new Guid(guidBytes);
            return guid;
        }

        public System.Random GetRandom(SeedLifetime seedLifetime, string seed)
        {
            var actualSeed = _hashService.Murmur3(seed);
            var generator = GetSeed(seedLifetime, actualSeed);
            return generator.Random;
        }

        public int Range(SeedLifetime seedLifetime, int seed, int minInclusive, int maxExclusive)
        {
            var generator = GetSeed(seedLifetime, seed);
            var result = generator.Random.Next(minInclusive, maxExclusive);
            return result;
        }

        public int Range(SeedLifetime seedLifetime, string seed, int minInclusive, int maxExclusive)
        {
            var actualSeed = _hashService.Murmur3(seed);
            return Range(seedLifetime, actualSeed, minInclusive, maxExclusive);
        }

        public float Range(SeedLifetime seedLifetime, string seed, float minInclusive, float maxExclusive)
        {
            var actualSeed = _hashService.Murmur3(seed);
            var generator = GetSeed(seedLifetime, actualSeed);
            var result = generator.Random.NextFloat(minInclusive, maxExclusive);
            return result;
        }

        private Seed GetSeed(SeedLifetime seedLifetime, int seed)
        {
            var seedGenerator = _seedGenerators.GetOrAdd(seed, k => new Seed { Lifetime = seedLifetime, Random = new System.Random(k) });
            return seedGenerator;
        }

        public void ResetUniqueIdCounters(string gameId)
        {
            _entityCounters.TryRemove(gameId, out _);
        }

        public void ResetSeedGenerators(params SeedLifetime[] lifetimes)
        {
            var lifetimesToRemove = lifetimes.ToHashSet();

            var seedsToRemove = _seedGenerators.Where(x => lifetimesToRemove.Contains(x.Value.Lifetime)).ToList();
            foreach (var seed in seedsToRemove)
            {
                _seedGenerators.TryRemove(seed.Key, out _);
            }

            _logger.LogInformation("Seeds generators have been cleared. LifetimeTypes={LifetimeTypes}", lifetimesToRemove);
        }

        public string GenerateUniqueId(UniqueIdType uniqueIdType, string gameId, string identifier)
        {
            try
            {
                var counter = _entityCounters.GetOrAdd(gameId ?? "main-menu", k => new UniqueIdCounters());
                var fullIdentifier = uniqueIdType + identifier;
                var hashed = _hashService.Murmur3(fullIdentifier).ToString();

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
