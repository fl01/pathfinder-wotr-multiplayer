using WOTRMultiplayer.MP.Entities.Rolls;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IRollStorage
    {
        void Add(RollDice rollDice);

        RollDice Get(int rollId, int playerId);

        int GetUniqueId(RollDice roll);
        void Reset();
    }
}
