using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkClick
    {
        public NetworkVector3 WorldPosition { get; set; }

        public string TargetUnitId { get; set; }

        public int Button { get; set; }

        public bool MuteEvents { get; set; }

        public List<string> SelectedUnits { get; set; } = [];

        public List<NetworkVector3> VectorPath { get; set; } = [];

        public NetworkAbility Ability { get; set; }
    }
}
