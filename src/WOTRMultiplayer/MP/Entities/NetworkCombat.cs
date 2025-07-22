using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkCombat
    {
        private int _round = 0;

        public bool IsInitialized { get; set; }

        public int Round
        {
            // sometimes combat is being paused (on client) before round started event, but still we need to initialize everything as first round aka 1
            get { return _round == 0 ? 1 : _round; }
            set { _round = value; }
        }
        public NetworkCombatTurn Turn { get; set; }

        public ConcurrentDictionary<long, bool> PlayersCombatInitialization { get; set; } = new();

        /// <summary>
        /// key: round+unitid
        /// </summary>
        public ConcurrentDictionary<string, HashSet<long>> PlayersTurnStartInitialization { get; set; } = new();

        /// <summary>
        /// key: round+unitid
        /// </summary>
        public ConcurrentDictionary<string, HashSet<long>> PlayersTurnEndInitialization { get; set; } = new();
    }
}
