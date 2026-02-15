using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.MapObjects
{
    public class NetworkOvertip
    {
        public NetworkMapObject MapObject { get; set; }

        public List<string> Units { get; set; } = [];

        public List<NetworkVector3> VectorPath { get; set; } = [];
    }
}
