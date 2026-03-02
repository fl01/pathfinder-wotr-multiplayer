using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkCombatTurn
    {
        public int? Seed { get; set; }

        public string UnitId { get; set; }

        public bool IsAI { get; set; }

        public bool IsLocalPlayer { get; set; }

        public bool IsActingInSurpriseRound { get; set; }

        public NetworkCombatTurnStage Stage { get; set; }

        public List<NetworkAIAction> AIActions { get; set; } = [];

        public HashSet<long> PlayersEndTurnInitialization { get; set; } = [];

        public HashSet<long> PlayersEndTurnSynchronization { get; set; } = [];

        /// <summary>
        /// TODO: might need to expand to track exact reasons why turn is locked (cannot be ended)
        /// </summary>
        public int LockCounter { get; set; }
    }
}
