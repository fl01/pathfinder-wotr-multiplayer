using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Services.Random
{
    public class DeterministicValueGenerator : IValueGenerator
    {
        private readonly ConcurrentDictionary<string, IdCounters> _entityCounters = new();
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

        public Guid CreateGuid(IdentifierLifetime lifetime, string seed)
        {
            var actualSeed = _hashService.Murmur3(seed);
            var generator = GetSeededGenerator(lifetime, actualSeed);
            var guidBytes = new byte[16];
            generator.Random.NextBytes(guidBytes);
            var guid = new Guid(guidBytes);
            return guid;
        }

        public NetworkVector2 GetRandomUnitCircle(IdentifierLifetime lifetime, string seed)
        {
            var actualSeed = _hashService.Murmur3(seed);
            var generator = GetSeededGenerator(lifetime, actualSeed);

            float angle = generator.Random.NextFloat(0, 1f) * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(generator.Random.NextFloat(0, 1f));

            var x = Mathf.Cos(angle) * radius;
            var y = Mathf.Sin(angle) * radius;

            var point = new NetworkVector2(x, y);
            return point;
        }

        public System.Random GetRandom(IdentifierLifetime lifetime, string identifier)
        {
            var actualSeed = CreateSeed(lifetime, identifier);
            var generator = GetSeededGenerator(lifetime, actualSeed);
            return generator.Random;
        }

        public int Range(IdentifierLifetime lifetime, string identifier, int minInclusive, int maxExclusive)
        {
            var actualSeed = CreateSeed(lifetime, identifier);
            var generator = GetSeededGenerator(lifetime, actualSeed);
            var result = generator.Random.Next(minInclusive, maxExclusive);
            return result;
        }

        public float Range(IdentifierLifetime lifetime, string identifier, float minInclusive, float maxExclusive)
        {
            var actualSeed = CreateSeed(lifetime, identifier);
            var generator = GetSeededGenerator(lifetime, actualSeed);
            var result = generator.Random.NextFloat(minInclusive, maxExclusive);
            return result;
        }

        private int CreateSeed(IdentifierLifetime lifetime, string identifier)
        {
            var fullIdentifier = lifetime.ToString() + identifier;
            var seed = _hashService.Murmur3(fullIdentifier);
            return seed;
        }

        private Seed GetSeededGenerator(IdentifierLifetime lifetime, int seed)
        {
            var seedGenerator = _seedGenerators.GetOrAdd(seed, k => new Seed { Lifetime = lifetime, Random = new System.Random(k) });
            return seedGenerator;
        }

        public void ResetUniqueIdCounters(string gameId)
        {
            _entityCounters.TryRemove(gameId, out _);
        }

        public void ResetSeededGenerators(params IdentifierLifetime[] lifetimes)
        {
            var lifetimesToRemove = lifetimes.ToHashSet();

            var seedsToRemove = _seedGenerators.Where(x => lifetimesToRemove.Contains(x.Value.Lifetime)).ToList();
            foreach (var seed in seedsToRemove)
            {
                _seedGenerators.TryRemove(seed.Key, out _);
            }

            _logger.LogInformation("Seeded generators have been cleared. Lifetimes={Lifetimes}", lifetimesToRemove);
        }

        public string GenerateUniqueId(IdType idType, string gameId, string identifier)
        {
            try
            {
                var counter = _entityCounters.GetOrAdd(gameId ?? "main-menu", k => new IdCounters());
                var fullIdentifier = idType + identifier;
                var hashed = _hashService.Murmur3(fullIdentifier).ToString();

                if (!counter.NameIdentifiers.TryGetValue(hashed, out uint nameCounter))
                {
                    nameCounter = 0;
                }

                var prefix = idType.GetAttributeOfType<DescriptionAttribute>()?.Description ?? idType.ToString();
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
                _logger.LogError(ex, "Unable to generate unique id. UniqueIdType={UniqueIdType}, Identifier={Identifier}", idType, identifier);
                throw;
            }
        }
    }
}
