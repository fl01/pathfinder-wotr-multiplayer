using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Combat.Crusades
{
    public class NetworkArmyCombatTurn : NetworkCombatTurnBase
    {
        public int Number { get; set; }

        public string UnitId { get; set; }

        public bool IsAI { get; set; }

        public HashSet<long> SyncedPlayers { get; set; } = [];

        public bool IsSyncRequired { get; set; }
    }
}
