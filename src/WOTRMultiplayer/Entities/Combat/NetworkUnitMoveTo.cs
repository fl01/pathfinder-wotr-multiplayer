using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkUnitMoveTo
    {
        public string InitiatorUnitId { get; set; }

        public List<NetworkVector3> VectorPath { get; set; } = [];

        public NetworkVector3 Destination { get; set; }

        public string MovementLimit { get; set; }

        public float? Orientation { get; set; }

        public float MovementDelay { get; set; }
    }
}
