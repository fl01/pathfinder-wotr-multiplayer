using System.Collections.Concurrent;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.Random
{
    public class PredictableUniqueIdGenerator : IUniqueIdGenerator
    {
        private readonly ConcurrentDictionary<string, UniqueIdCounters> _counters = new();
        private readonly ILogger<PredictableUniqueIdGenerator> _logger;
        private readonly IGameInteractionService _gameInteractionService;
        private readonly IHashService _hashService;

        public PredictableUniqueIdGenerator(
            ILogger<PredictableUniqueIdGenerator> logger,
            IGameInteractionService gameInteractionService,
            IHashService hashService)
        {
            _logger = logger;
            _gameInteractionService = gameInteractionService;
            _hashService = hashService;
        }

        public void Reset(string gameId)
        {
            _counters.TryRemove(gameId, out _);
            _logger.LogInformation("Counters have been cleared. GameId={gameId}", gameId);
        }

        public string GenerateId(UniqueIdType uniqueIdType, string gameId, string identifier)
        {
            try
            {
                var counter = _counters.GetOrAdd(gameId ?? "main-menu", k => new UniqueIdCounters());
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
                _logger.LogError(ex, "Unable to generate unique id. Type={type}, Identifier={identifier}", uniqueIdType, identifier);
                throw;
            }
        }
    }
}
