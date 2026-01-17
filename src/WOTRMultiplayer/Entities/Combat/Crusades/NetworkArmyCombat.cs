using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Combat.Crusades
{
    public class NetworkArmyCombat
    {
        public int Seed { get; set; }

        public bool IsInitialized { get; set; }

        public NetworkArmyCombatTurn Turn { get; set; }

        public ConcurrentDictionary<long, bool> PlayersCombatInitialization { get; set; } = new();

        public List<NetworkAIAction> AIActions { get; set; } = [];
    }
}
