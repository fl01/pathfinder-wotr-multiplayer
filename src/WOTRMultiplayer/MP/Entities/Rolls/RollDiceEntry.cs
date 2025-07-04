using System.Collections.Concurrent;

namespace WOTRMultiplayer.MP.Entities.Rolls
{
    public class RollDiceEntry
    {
        public RollDice Roll { get; set; }

        public ConcurrentDictionary<int, int> RetrieveHistory { get; set; } = new();

        public RollDiceEntry(RollDice rollDice)
        {
            Roll = rollDice;
        }
    }
}
