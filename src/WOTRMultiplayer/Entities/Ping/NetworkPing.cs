using WOTRMultiplayer.Entities.MapObjects;

namespace WOTRMultiplayer.Entities.Ping
{
    public class NetworkPing
    {
        public NetworkVector3 WorldPosition { get; set; }

        public string UnitId { get; set; }

        public NetworkMapObject MapObject { get; set; }

        public NetworkPingType Type { get; set; }
    }
}
