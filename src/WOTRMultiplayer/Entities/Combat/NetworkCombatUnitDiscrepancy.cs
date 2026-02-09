using System.Collections.Generic;
using WOTRMultiplayer.Entities.Units;

namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkCombatUnitDiscrepancy
    {
        public Dictionary<long, List<NetworkUnit>> Units { get; set; } = [];
    }
}
