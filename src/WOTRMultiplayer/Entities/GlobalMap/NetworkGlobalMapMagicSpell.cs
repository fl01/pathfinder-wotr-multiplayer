using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.GlobalMap
{
    public class NetworkGlobalMapMagicSpell
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public List<string> TargetArmies { get; set; } = [];

        public NetworkGlobalMapLocation Location { get; set; }
    }
}
