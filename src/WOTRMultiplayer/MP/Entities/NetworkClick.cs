using System.Numerics;

namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkClick
    {
        public Vector3 WorldPosition { get; set; }

        public string TargetUnitId { get; set; }

        public int Button { get; set; }

        public bool MuteEvents { get; set; }
    }
}
