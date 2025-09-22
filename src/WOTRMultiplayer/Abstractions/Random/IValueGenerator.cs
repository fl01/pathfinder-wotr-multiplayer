using WOTRMultiplayer.Random;

namespace WOTRMultiplayer.Abstractions.Random
{
    public interface IValueGenerator
    {
        string GenerateUniqueId(UniqueIdType uniqueIdType, string gameId, string identifier);

        int Range(SeedLifetime seedLifetime, int seed, int minInclusive, int maxExclusive);

        int Range(SeedLifetime seedLifetime, string seed, int minInclusive, int maxExclusive);

        float Range(SeedLifetime seedLifetime, string seed, float minInclusive, float maxExclusive);

        void ResetUniqueIdCounters(string gameId);

        void ResetSeedGenerators(params SeedLifetime[] lifetimes);
    }
}
