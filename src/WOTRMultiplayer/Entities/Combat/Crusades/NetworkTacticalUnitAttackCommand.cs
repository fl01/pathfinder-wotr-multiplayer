using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Combat.Crusades
{
    public class NetworkTacticalUnitAttackCommand
    {
        public string UnitId { get; set; }

        public List<NetworkVector3> Path { get; set; }

        public string TargetUnitId { get; set; }
    }
}
