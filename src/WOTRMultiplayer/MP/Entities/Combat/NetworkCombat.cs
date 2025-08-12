using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities.Combat
{
    public class NetworkCombat
    {
        private int _round = 0;

        public bool IsInitialized { get; set; }

        public int CombatPreparedFrames { get; set; }

        public int Round
        {
            // sometimes combat is being paused (on client) before round started event, but we need to initialize everything as first round
            get { return _round == 0 ? 1 : _round; }
            set { _round = value; }
        }

        public NetworkCombatTurn Turn { get; set; }

        public ConcurrentDictionary<long, bool> PlayersCombatInitialization { get; set; } = new();

        public ConcurrentDictionary<string, HashSet<long>> PlayersNextTurnInitialization { get; set; } = new();

        public ConcurrentDictionary<string, HashSet<long>> PlayersNextTurnSynchronization { get; set; } = new();

        public ConcurrentDictionary<string, HashSet<long>> MidCombatUnitJoins { get; set; } = new();

        public HashSet<string> ConfirmedMidCombatUnits { get; set; } = [];

        public List<NetworkAIAction> AIActions { get; set; } = [];
    }
}
