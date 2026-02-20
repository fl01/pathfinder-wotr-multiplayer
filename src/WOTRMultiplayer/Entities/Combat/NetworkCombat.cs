using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using WOTRMultiplayer.Entities.AreaEffects;
using WOTRMultiplayer.Entities.Units;

namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkCombat
    {
        /// <summary>
        /// new seed for each combat
        /// </summary>
        public int Seed { get; set; }

        public NetworkCombatStage Stage { get; set; }

        public bool IsInitialized { get; set; }

        public bool IsPrepared { get; set; }

        public bool IsPlaying { get; set; }

        public bool IsRecovering { get; set; }

        public int Round { get; set; }

        public NetworkCombatTurn Turn { get; set; }

        public ConcurrentDictionary<long, bool> PlayersCombatInitialization { get; set; } = new();

        public ConcurrentDictionary<long, List<NetworkUnit>> PlayersCombatPreparation { get; set; } = new();

        public ConcurrentDictionary<string, HashSet<long>> PlayersNextTurnInitialization { get; set; } = new();

        public ConcurrentDictionary<string, HashSet<long>> PlayersNextTurnSynchronization { get; set; } = new();

        public ConcurrentDictionary<string, HashSet<long>> MidCombatUnitJoins { get; set; } = new();

        public HashSet<string> ConfirmedMidCombatUnits { get; set; } = [];

        public HashSet<string> UntargetableUnits { get; set; } = [];

        public HashSet<NetworkAreaEffect> TriggeredAreaEffects { get; set; } = [];

        public DateTime StartedAt { get; set; }
    }
}
