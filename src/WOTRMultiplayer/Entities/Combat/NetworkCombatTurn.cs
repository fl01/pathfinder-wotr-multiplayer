using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkCombatTurn : NetworkCombatTurnBase
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
    }
}
