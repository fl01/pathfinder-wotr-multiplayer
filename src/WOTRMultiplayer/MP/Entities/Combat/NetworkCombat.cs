using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities.Combat
{
    public class NetworkCombat
    {
        public bool IsInitialized { get; set; }

        public int CombatPreparedFrames { get; set; }

        public int Round { get; set; }

        public NetworkCombatTurn Turn { get; set; }

        public ConcurrentDictionary<long, bool> PlayersCombatInitialization { get; set; } = new();

        public ConcurrentDictionary<string, HashSet<long>> PlayersNextTurnInitialization { get; set; } = new();

        public ConcurrentDictionary<string, HashSet<long>> PlayersNextTurnSynchronization { get; set; } = new();

        public ConcurrentDictionary<string, HashSet<long>> MidCombatUnitJoins { get; set; } = new();

        public HashSet<string> ConfirmedMidCombatUnits { get; set; } = [];

        public List<NetworkAIAction> AIActions { get; set; } = [];
    }
}
