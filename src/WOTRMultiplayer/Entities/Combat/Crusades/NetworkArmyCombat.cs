using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Combat.Crusades
{
    public class NetworkArmyCombat
    {
        /// <summary>
        /// Determined by the game. It's mostly based on combat area coordinates
        /// </summary>
        public int AreaSeed { get; set; }

        /// <summary>
        /// Random int created on combat initialization
        /// </summary>
        public int Seed { get; set; }

        public bool IsInitialized { get; set; }

        public NetworkArmyCombatTurn Turn { get; set; }

        public ConcurrentDictionary<long, bool> PlayersCombatInitialization { get; set; } = new();

        public ConcurrentDictionary<int, HashSet<long>> PlayersNextTurnInitialization { get; set; } = new();

        public HashSet<string> RemotelyKilledUnits { get; set; } = [];
    }
}
