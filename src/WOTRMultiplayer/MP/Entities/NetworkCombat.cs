using System.Collections.Concurrent;

namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkCombat
    {
        public bool IsInitialized { get; set; }

        public int Round { get; set; }

        public ConcurrentDictionary<long, bool> PlayersInitialization { get; set; } = new();

        public string TurnOwner { get; set; }

        public bool IsAITurn { get; set; }

        public bool IsMyTurn { get; set; }
        public bool IsActingInSurpriseRound { get; set; }
    }
}
