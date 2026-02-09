using System;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.Abstractions.Random
{
    public interface IValueGenerator
    {
        string GenerateUniqueId(IdType idType, string gameId, string identifier);

        int Range(IdentifierLifetime lifetime, string identifier, int minInclusive, int maxExclusive);

        float Range(IdentifierLifetime lifetime, string identifier, float minInclusive, float maxExclusive);

        void ResetUniqueIdCounters(string gameId);

        void ResetSeededGenerators(params IdentifierLifetime[] lifetimes);

        Guid CreateGuid(IdentifierLifetime area, string identifier);

        System.Random GetRandom(IdentifierLifetime lifetime, string identifier);

        NetworkVector2 GetRandomUnitCircle(IdentifierLifetime lifetime, string identifier);
    }
}
