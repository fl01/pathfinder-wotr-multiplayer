using System.Collections.Generic;
using WOTRMultiplayer.MP.Entities.Units;

namespace WOTRMultiplayer.MP.Entities.Combat
{
    public class NetworkCombatState
    {
        public int RoundNumber { get; set; }

        public bool HasSurpriseRound { get; set; }

        public List<NetworkUnit> Units { get; set; } = [];
    }
}
