using System.Collections.Generic;
using WOTRMultiplayer.Entities.Units;

namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkCombatState
    {
        public int RoundNumber { get; set; }

        public bool HasSurpriseRound { get; set; }

        public List<NetworkUnit> Units { get; set; } = [];

        public List<string> KilledUnits { get; set; } = [];
    }
}
