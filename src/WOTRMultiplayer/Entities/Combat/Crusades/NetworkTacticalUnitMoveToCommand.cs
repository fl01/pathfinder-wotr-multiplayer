using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Combat.Crusades
{
    public class NetworkTacticalUnitMoveToCommand
    {
        public string UnitId { get; set; }

        public List<NetworkVector3> Path { get; set; }
    }
}
