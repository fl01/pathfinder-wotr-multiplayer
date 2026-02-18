using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Units
{
    public class NetworkUnitCombatState
    {
        public List<string> EngagedUnits { get; set; } = [];

        public List<string> EngagedBy { get; set; } = [];

        public bool NotSurprised { get; set; }
    }
}
