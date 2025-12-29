using System.Collections.Generic;

namespace WOTRMultiplayer.Entities
{
    public class NetworkClick
    {
        public NetworkVector3 WorldPosition { get; set; }

        public string TargetUnitId { get; set; }

        public string MapObjectId { get; set; }

        public bool IsLootBagMapObject { get; set; }

        public int Button { get; set; }

        public bool MuteEvents { get; set; }

        public List<string> SelectedUnits { get; set; } = [];

        public List<NetworkVector3> VectorPath { get; set; } = [];

        public bool IsTMBClick { get; set; }

        public string MovementLimit { get; set; }
    }
}
