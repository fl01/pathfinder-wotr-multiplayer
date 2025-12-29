using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Leveling
{
    public class NetworkLeveling
    {
        public string UnitId { get; set; }

        public NetworkLevelingType Type { get; set; }

        public HashSet<long> PlayerReadiness { get; set; } = [];

        public int PhaseIndex { get; set; }

        public NetworkLeveling(string unitId, NetworkLevelingType levelingType)
        {
            UnitId = unitId;
            Type = levelingType;
        }
    }
}
