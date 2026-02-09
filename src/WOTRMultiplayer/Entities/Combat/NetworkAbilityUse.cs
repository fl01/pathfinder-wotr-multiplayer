using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkAbilityUse
    {
        public NetworkAbility Ability { get; set; }

        public string InitiatorUnitId { get; set; }

        public NetworkTargetWrapper Target { get; set; }

        public List<NetworkVector3> VectorPath { get; set; }

        public string CommandType { get; set; }

        public string MovementLimit { get; set; }
    }
}
