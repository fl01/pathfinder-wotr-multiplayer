using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities.Loot
{
    public class NetworkLootContainer
    {
        public string Id { get; set; }

        public NetworkVector3 Position { get; set; }

        public bool IsMapObject { get; set; }

        public List<NetworkLootItem> Items { get; set; } = [];

    }
}
