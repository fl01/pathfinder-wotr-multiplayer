using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities.MapObjects
{
    public class NetworkOvertip
    {
        public NetworkMapObject MapObject { get; set; }

        public List<string> Units { get; set; } = [];

        public bool RequiresEveryoneToMoveMove { get; set; }
    }
}
