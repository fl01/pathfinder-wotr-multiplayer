using WOTRMultiplayer.Random;

namespace WOTRMultiplayer.Abstractions.Random
{
    public interface IUniqueIdGenerator
    {
        string GenerateId(UniqueIdType uniqueIdType, string gameId, string identifier);

        void Reset(string gameId);
    }
}
