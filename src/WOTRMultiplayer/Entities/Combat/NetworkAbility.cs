using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkAbility
    {
        public string Id { get; set; }

        public string SpellbookId { get; set; }

        public string CasterId { get; set; }

        public NetworkTargetWrapper Target { get; set; }

        public List<NetworkVector3> VectorPath { get; set; }

        public string CommandType { get; set; }

        public string Name { get; set; }

        public string ConvertedFromId { get; set; }

        public string MovementLimit { get; set; }
    }
}
